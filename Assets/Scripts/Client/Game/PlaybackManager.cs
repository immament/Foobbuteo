// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections;
using Imma.Common;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Contract;
using Unity.Netcode;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Game
{
    public class PlaybackManager : BaseMonoBehaviour
    {
        private const float k_minDurationToAnimate = 0.001f;
        private const float k_minSqrDistanceToAnimate = 0.001f;

        [SerializeField]
        private AnimationCurve m_passHeightCurve = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.45f, 1f),
            new Keyframe(0.9f, 0f),
            new Keyframe(1f, 0f)
        );

        // image width / pixels per unit
        private const float k_ballWidth = 16f / 200f; //
        private const float k_ballCircumference = Mathf.PI * k_ballWidth;
        private const float k_ballRotationSpeed = 45f;

        [SerializeField]
        private float m_passBallMinScale = 1f;

        [SerializeField]
        private float m_passBallMaxScale = 2f;

        [SerializeField]
        private AnimationCurve m_shotHeightCurve = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0f)
        );

        [SerializeField]
        private float m_shotBallMinScale = 1f;

        [SerializeField]
        private float m_shotBallMaxScale = 1.2f;

        public IEnumerator Play(
            TurnResultReadyMessage msg,
            System.Action<int, int, MatchCommentaryCue> commentary = null,
            MatchSoundService sounds = null
        )
        {
            this.LDebug(() => $"Play events: {msg.Result.Events?.Length ?? 0}");
            if (msg.Result.Events != null)
            {
                for (int index = 0; index < msg.Result.Events.Length; index++)
                {
                    EmitCommentary(msg, index, false, commentary);
                    bool goal = MatchSoundService.IsGoalEvent(msg, index);
                    yield return PlayEvent(msg.Result.Events[index], msg.StageEndTime, sounds, goal);
                    EmitCommentary(msg, index, true, commentary);
                }
            }
        }

        public static void EmitCommentary(
            TurnResultReadyMessage msg,
            int eventIndex,
            bool afterEvent,
            System.Action<int, int, MatchCommentaryCue> callback
        )
        {
            var cues = msg.Result.Commentary;
            if (cues == null || callback == null)
                return;
            for (int index = 0; index < cues.Length; index++)
                if (cues[index].EventIndex == eventIndex && cues[index].AfterEvent == afterEvent)
                    callback(msg.TurnNumber, index, cues[index]);
        }

        private IEnumerator PlayEvent(MatchEvent matchEvent, float stageEndTime, MatchSoundService sounds, bool goal)
        {
            switch (matchEvent)
            {
                case PlayerMoveEvent playerMove:
                    yield return PlayerMove(playerMove, stageEndTime);
                    break;
                case BallMoveEvent ballMove:
                    yield return BallMove(ballMove, stageEndTime, sounds, goal);
                    break;
            }
        }

        private IEnumerator BallMove(BallMoveEvent ev, float stageEndTime, MatchSoundService sounds, bool goal)
        {
            this.LInfo(
                $"BallMove: {new { ev.EventType, ev.Duration, stageEndTime, NetworkManager.Singleton.ServerTime.TimeAsFloat }}"
            );

            Vector2 end = ev.ToPosition.LogicToWorld();
            var ball = GameManager.Instance.Ball;

            // check if is time for animation & if ball is not already on position
            if (ShouldAnimate(ev, stageEndTime, ball.transform.localPosition, end))
            {
                Vector2 start = ev.FromPosition.LogicToWorld();
                sounds?.BallAnimationStarted(ev.EventType);
                yield return BallMoveAnimation(ball, ev, start, end);
                sounds?.BallAnimationFinished(ev.EventType, goal);
            }
            ball.transform.localPosition = end;
            // this.LogInfo($"End: {ball.transform.localPosition}/{ball.transform.position} ");
        }

        private IEnumerator BallMoveAnimation(BallMB ball, BallMoveEvent ev, Vector2 start, Vector2 end)
        {
            float elapsed = 0f;
            float duration = ev.Duration;
            Vector3 orgBallScale = ball.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                Vector3 previousPosition = ball.transform.localPosition;
                ball.transform.localPosition = Vector3.Lerp(start, end, elapsed / duration);
                switch (ev.EventType)
                {
                    case MatchEventType.HighPass:
                    {
                        float scale = Mathf.Lerp(
                            m_passBallMinScale,
                            m_passBallMaxScale,
                            m_passHeightCurve.Evaluate(elapsed / duration)
                        );
                        ball.transform.localScale = Vector3.one * scale;
                        break;
                    }
                    case MatchEventType.Shot:
                    {
                        float scale = Mathf.Lerp(
                            m_shotBallMinScale,
                            m_shotBallMaxScale,
                            m_shotHeightCurve.Evaluate(elapsed)
                        );
                        ball.transform.localScale = Vector3.one * scale;
                        break;
                    }
                }
                RotateBasedOnMovement(ball.transform, previousPosition, ball.transform.localPosition);
                yield return null;
            }
            ball.transform.localScale = orgBallScale;
        }

        private void RotateBasedOnMovement(Transform a_transform, Vector3 prevPos, Vector3 currentPos)
        {
            float distance = Vector3.Distance(prevPos, currentPos);
            if (distance > 0.001f)
            {
                float rotationAmount = (distance / k_ballCircumference) * k_ballRotationSpeed;
                float directionMultiplier = (currentPos.x > prevPos.x) ? 1f : -1f;
                a_transform.Rotate(Vector3.forward, directionMultiplier * rotationAmount, Space.Self);
            }
        }

        private IEnumerator PlayerMove(PlayerMoveEvent moveEvent, float stageEndTime)
        {
            this.LTrace(moveEvent);

            var playerMB = GameManager.Instance.TeamsRegistry.GetPlayer(moveEvent.TeamIdx, moveEvent.PlayerIdx);

            Vector2 end = moveEvent.ToPosition.LogicToWorld();

            // check if is time for animation & if player is not already on position
            if (ShouldAnimate(moveEvent, stageEndTime, playerMB.transform.localPosition, end))
            {
                Vector2 start = moveEvent.FromPosition.LogicToWorld();
                yield return PlayerMoveAnimation(playerMB, moveEvent, start, end);
            }
            // else { }

            playerMB.transform.localPosition = end;
        }

        private bool ShouldAnimate(MatchEvent moveEvent, float stageEndTime, Vector2 currentPosition, Vector2 end)
        {
            if (
                moveEvent.Duration > k_minDurationToAnimate
                && IsEnoughTime(moveEvent.Duration, stageEndTime)
                && (end - currentPosition).sqrMagnitude > k_minSqrDistanceToAnimate
            )
                return true;
            this.LDebug(() =>
                $"[{moveEvent.EventType}] Skipped animation: {moveEvent.Duration > k_minDurationToAnimate}, {IsEnoughTime(moveEvent.Duration, stageEndTime)}, {(end - currentPosition).sqrMagnitude > k_minSqrDistanceToAnimate}"
            );
            return false;
        }

        private static bool IsEnoughTime(float duration, float stageEndTime)
        {
            return NetworkManager.Singleton.ServerTime.TimeAsFloat + duration < stageEndTime;
        }

        private static IEnumerator PlayerMoveAnimation(
            PlayerMB playerView,
            PlayerMoveEvent moveEvent,
            Vector2 start,
            Vector2 end
        )
        {
            Vector3 ballOffset = moveEvent.WithBall
                ? ballOffset = Vector3.Normalize(end - start) * 0.05f
                : Vector3.zero;

            float elapsed = 0f;
            float duration = moveEvent.Duration;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percentTime = elapsed / duration;
                playerView.transform.localPosition = Vector3.Lerp(start, end, percentTime);
                if (moveEvent.WithBall)
                {
                    GameManager.Instance.Ball.transform.localPosition = playerView.transform.localPosition + ballOffset;
                }
                yield return null;
            }
        }
    }
}

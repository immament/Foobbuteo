// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using Imma.Foobbuteo.Shared.Game;
using Imma.Foobbuteo.Shared.Game.Model;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameRoomNB))]
public sealed class GameRoomNBEditor : BaseBehaviourEditor
{
    private TeamIndex m_teamIdx = TeamIndex.Home;
    private int m_playerIdx = 1;
    private Vector2 m_playerPosition;
    private Vector2 m_ballPosition = PitchUtils.PitchBounds.Center;
    private string m_message;
    private MessageType m_messageType = MessageType.Info;

    private void OnEnable()
    {
        if (!Application.isPlaying || target is not GameRoomNB gameRoom)
            return;

        ReadPlayerPosition(gameRoom);
        ReadBallPosition(gameRoom);
        m_message = null;
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();
        bool serverStateChanged = EditorGUI.EndChangeCheck();
        var gameRoom = (GameRoomNB)target;
        if (Application.isPlaying && serverStateChanged)
        {
            gameRoom.EditorRefreshPositioningBounds();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Server formation preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Player moves are queued and sent through the normal turn result. Ball moves apply immediately and refresh formation gizmos.",
            MessageType.Info
        );

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUI.BeginChangeCheck();
            m_teamIdx = (TeamIndex)EditorGUILayout.EnumPopup("Team", m_teamIdx);
            m_playerIdx = EditorGUILayout.IntSlider("Player", m_playerIdx, 1, 11);
            if (EditorGUI.EndChangeCheck())
                ReadPlayerPosition(gameRoom);

            m_playerPosition = EditorGUILayout.Vector2Field("Player position", m_playerPosition);
            if (GUILayout.Button("Read player position"))
                ReadPlayerPosition(gameRoom);
            if (GUILayout.Button("Queue player move"))
                SetResult(gameRoom.EditorQueuePlayerMove(m_teamIdx, (byte)m_playerIdx, m_playerPosition));

            EditorGUILayout.Space();
            m_ballPosition = EditorGUILayout.Vector2Field("Ball position", m_ballPosition);
            if (GUILayout.Button("Read ball position"))
                ReadBallPosition(gameRoom);
            if (GUILayout.Button("Move ball and refresh bounds"))
            {
                SetResult(gameRoom.EditorMoveBall(m_ballPosition));
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to edit the server game.", MessageType.Warning);
        if (!string.IsNullOrEmpty(m_message))
            EditorGUILayout.HelpBox(m_message, m_messageType);
    }

    private void ReadPlayerPosition(GameRoomNB gameRoom)
    {
        if (gameRoom.EditorTryGetPlayerPosition(m_teamIdx, (byte)m_playerIdx, out var position))
        {
            m_playerPosition = position;
            SetResult(null, "Player position loaded.");
        }
        else
        {
            SetResult("Server game is not initialized.");
        }
    }

    private void ReadBallPosition(GameRoomNB gameRoom)
    {
        if (gameRoom.EditorTryGetBallPosition(out var position))
        {
            m_ballPosition = position;
            SetResult(null, "Ball position loaded.");
        }
        else
        {
            SetResult("Server game is not initialized.");
        }
    }

    private void SetResult(string error, string success = "Change applied.")
    {
        m_message = error ?? success;
        m_messageType = error == null ? MessageType.Info : MessageType.Error;
        Repaint();
    }
}

// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;
using Imma.Foobbuteo.Client.Game.Model;
using Imma.Foobbuteo.Client.Game.UI.VM;
using Imma.Foobbuteo.Common;
using Imma.Foobbuteo.Shared.Game;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Game
{
    [RequireComponent(typeof(GameManager))]
    public class TeamSpawner : BaseMonoBehaviour
    {
        private readonly List<GameObject> m_spawnedPlayers = new();

        [SerializeField]
        private PlayerSpriteDatabase m_SpriteDB;

        [SerializeField]
        private GameObject m_playerPrefab;

        [SerializeField]
        private GameManager m_gameManager;

        [SerializeField]
        private Transform m_pitch;

        // private void Awake()
        // {
        //     m_PlayerSpriteDatabase = GetComponent<PlayerSpriteDatabase>();
        //     // _gameManager = GetComponent<GameManager>();
        // }

        internal void Spawn(TeamVM teamA, TeamVM teamB, ClientGameState gameState)
        {
            foreach (var player in m_spawnedPlayers)
            {
                if (player != null)
                {
                    player.SetActive(false);
                    Destroy(player);
                }
            }
            m_spawnedPlayers.Clear();
            m_gameManager.TeamsRegistry.Clear();
            SpawnTeam(m_pitch, teamA, gameState);
            SpawnTeam(m_pitch, teamB, gameState);
        }

        private void SpawnTeam(Transform parent, TeamVM team, ClientGameState gameState)
        {
            // players loop
            for (byte idx = 1; idx <= 11; idx++)
            {
                ClientPlayerState playerState = gameState.GetPlayer(team.TeamIdx, idx);
                Vector2 pos = playerState.Position.LogicToWorld();

                GameObject playerObj = Instantiate(m_playerPrefab, parent);
                m_spawnedPlayers.Add(playerObj);

                playerObj.transform.SetLocalPositionAndRotation(pos, Quaternion.identity);
                playerObj.name = $"Player_{team.TeamIdx}_{idx}";

                PlayerMB playerScript = playerObj.GetComponent<PlayerMB>();

                var vm = new PlayerVM(idx, team, playerState);
                playerScript.Init(m_gameManager, m_SpriteDB, vm, playerState);

                if (m_gameManager != null)
                    m_gameManager.TeamsRegistry.RegisterPlayer(team.TeamIdx, playerScript);
            }
        }
    }
}

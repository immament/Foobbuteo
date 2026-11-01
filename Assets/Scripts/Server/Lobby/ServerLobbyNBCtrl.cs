// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

// using Imma.Common;
// using Imma.Foobbuteo.Common;
// using Imma.Foobbuteo.Shared;
// using Imma.Foobbuteo.Shared.Lobby;
// using Imma.Foobbuteo.Shared.Lobby.Contract;
// using Unity.Netcode;
// using UnityEngine;

// namespace Imma.Foobbuteo.Server.Lobby
// {
//     public class ServerLobbyNBCtrl : IServerLobbyNBCtrl
//     {
//         private LobbyNB m_lobbyNB;
//         private FoobbuteoMainServer m_foobbuteoServer;

//         public void Init(LobbyNB lobbyNB)
//         {
//             this.LTrace($"InitServer {lobbyNB != null}");
//             if (m_lobbyNB != null)
//             {
//                 this.LError("InitServer already done. lobbyNB already set!");
//                 return;
//             }
//             m_lobbyNB = lobbyNB;

//             m_foobbuteoServer = CreateFoobbuteoServer(m_lobbyNB.Debug, m_lobbyNB.GamePrefab);
//         }

//         private FoobbuteoMainServer CreateFoobbuteoServer(bool debug, GameObject gamePrefab)
//         {
//             var server = new FoobbuteoMainServer(debug, gamePrefab);
//             ServiceLocator.Current.Register(server);
//             server.GameListChanged += OnGameListChanged;
//             return server;
//         }

//         private void OnGameListChanged()
//         {
//             m_lobbyNB.ToClient_SendServerInfoRpc(
//                 m_foobbuteoServer.GetServerInfoContract(),
//                 m_lobbyNB.RpcTarget.ClientsAndHost
//             );
//         }

//         public ServerInfoContract GetServerInfoContract()
//         {
//             return m_foobbuteoServer.GetServerInfoContract();
//         }

//         public bool AddClientToGame(ulong senderClientId, int gameId)
//         {
//             return m_foobbuteoServer.AddClientToGame(senderClientId, gameId);
//         }

//         public void DeleteGame(int gameId)
//         {
//             m_foobbuteoServer.DeleteGame(gameId);
//         }

//         public void Dispose()
//         {
//             this.LTrace("--");
//             if (m_foobbuteoServer != null)
//                 m_foobbuteoServer.GameListChanged -= OnGameListChanged;
//             ServiceLocator.Current.Unregister<FoobbuteoMainServer>();
//         }

//         public UserSessionData GetUserInfo(ulong senderClientId)
//         {
//             return m_foobbuteoServer.GetUserInfo(senderClientId);
//         }

//         public int CreateNewGame(ulong clientId)
//         {
//             return m_foobbuteoServer.CreateNewGame(clientId);
//         }
//     }
// }

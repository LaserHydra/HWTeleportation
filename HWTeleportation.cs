//Reference: UnityEngine.UI

using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    using HomeData = Dictionary<ulong, Dictionary<string, Vector3>>;
    using WarpData = Dictionary<string, Vector3>;
    using LastUsageDictionary = Dictionary<ulong, DateTime>;

    [Info("HW Teleportation", "LaserHydra", "1.6.1")]
    [Description("Implements different teleportation features such as warps & homes")]
    internal class HWTeleportation : HurtworldPlugin
    {
        #region Fields

        private const string LocationWildcard = "~";

        private Configuration _config;

        private HomeData _homes = new HomeData();
        private WarpData _warps = new WarpData();

        private Dictionary<PlayerSession, PlayerSession> _pendingRequests = new Dictionary<PlayerSession, PlayerSession>();
        private Dictionary<PlayerSession, Timer> _pendingTimers = new Dictionary<PlayerSession, Timer>();

        private LastUsageDictionary _lastTpr = new LastUsageDictionary();
        private LastUsageDictionary _lastHome = new LastUsageDictionary();
        private LastUsageDictionary _lastWarp = new LastUsageDictionary();

        #endregion

        #region Hooks

        private void Loaded()
        {
            permission.RegisterPermission($"{this.Name}.admin", this);
            permission.RegisterPermission($"{this.Name}.tpr", this);
            permission.RegisterPermission($"{this.Name}.home", this);
            permission.RegisterPermission($"{this.Name}.warp", this);

            LoadConfig();
            LoadData();

            foreach (var kvp in _config.Home.HomeLimitPermissions)
                permission.RegisterPermission(kvp.Key, this);
        }

        #endregion

        #region Home & Warp Data

        private void LoadData()
        {
            _homes = Interface.Oxide.DataFileSystem.ReadObject<HomeData>("Teleportation/Homes");
            _warps = Interface.Oxide.DataFileSystem.ReadObject<WarpData>("Teleportation/Warps");
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("Teleportation/Homes", _homes);
            Interface.GetMod().DataFileSystem.WriteObject("Teleportation/Warps", _warps);
        }

        #endregion

        #region Admin Teleportation

        [ChatCommand("tp")]
        private void CmdTeleport(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "admin"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            switch (args.Length)
            {
                case 1:

                    PlayerSession target = GetPlayer(args[0], player);

                    if (target == null)
                        return;

                    if(!IsSafePlayer(target))
                    {
                        SendChatMessage(player, GetMessage("Target Location Unsafe", player));
                        return;
                    }

                    TeleportToPlayer(player, target);
                    SendChatMessage(player, GetMessage("Teleported", player).Replace("{target}", target.Identity.Name));

                    break;

                case 2:

                    PlayerSession teleportPlayer = GetPlayer(args[0], player);
                    PlayerSession targetPlayer = GetPlayer(args[1], player);

                    if (targetPlayer == null || teleportPlayer == null)
                        return;

                    if(!IsSafePlayer(targetPlayer))
                    {
                        SendChatMessage(player, GetMessage("Target Location Unsafe", player));
                        return;
                    }

                    TeleportToPlayer(teleportPlayer, targetPlayer);
                    SendChatMessage(teleportPlayer, GetMessage("Teleported", teleportPlayer).Replace("{target}", targetPlayer.Identity.Name));

                    break;

                case 3:

                    float x = args[0] == LocationWildcard
                        ? player.WorldPlayerEntity.transform.position.x 
                        : Convert.ToSingle(args[0]);

                    float y = args[1] == LocationWildcard
                        ? player.WorldPlayerEntity.transform.position.y
                        : Convert.ToSingle(args[1]);

                    float z = args[2] == LocationWildcard
                        ? player.WorldPlayerEntity.transform.position.z
                        : Convert.ToSingle(args[2]);

                    if (!IsSafeLocation(new Vector3(x,y,z)))
                    {
                        SendChatMessage(player, GetMessage("Target Location Unsafe", player));
                        return;
                    }

                    TeleportToLocation(player, new Vector3(x, y, z));
                    SendChatMessage(player, GetMessage("Teleported", player).Replace("{target}", $"(X: {x}, Y: {y}, Z: {z})."));

                    break;

                default:

                    SendChatMessage(player, "/tp {target}\n/tp {player} {target}\n/tp {x} {y} {z}");

                    break;
            }
        }

        [ChatCommand("tphere")]
        private void CmdTeleportHere(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "admin"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (args.Length != 1)
            {
                SendChatMessage(player, "Syntax: /tphere {player}");
                return;
            }

            PlayerSession target = GetPlayer(args[0], player);

            if (target == null)
                return;

            if (target == player)
            {
                SendChatMessage(player, GetMessage("Teleport To Self", player));
                return;
            }

            TeleportToPlayer(target, player);

            SendChatMessage(target, 
                GetMessage("Teleported", target)
                    .Replace("{target}", player.Identity.Name)
            );
        }

        #endregion

        #region Homes

        [ChatCommand("removehome")]
        private void CmdRemoveHome(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "home"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (!_config.Home.Enabled)
                return;

            if (args.Length != 1)
            {
                SendChatMessage(player, "Syntax: /removehome {home}");
                return;
            }

            string home = args[0].ToLower();

            if (!GetHomes(player).Keys.Contains(home))
            {
                SendChatMessage(player, GetMessage("Unknown Home", player).Replace("{home}", home));
                return;
            }

            RemoveHome(player, home);
            SendChatMessage(player, GetMessage("Home Removed", player).Replace("{home}", home));
        }

        [ChatCommand("sethome")]
        private void CmdSetHome(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "home"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (!_config.Home.Enabled)
                return;

            if (_config.Home.CheckForStakes && !HasStakeAuthority(player))
            {
                SendChatMessage(player, GetMessage("No Stake", player));
                return;
            }

            if (args.Length != 1)
            {
                SendChatMessage(player, "Syntax: /sethome {home}");
                return;
            }

            string home = args[0].ToLower();

            if (GetHomes(player).Keys.Contains(home))
            {
                SendChatMessage(player, GetMessage("Home Exists", player).Replace("{home}", home));
                return;
            }
            
            if (GetHomeCount(player) >= GetHomeLimit(player))
            {
                SendChatMessage(player, GetMessage("Max Homes", player).Replace("{count}", GetHomeLimit(player).ToString()));
                return;
            }

            AddHome(player, home);
            SendChatMessage(player, GetMessage("Home Set", player).Replace("{home}", home));
        }

        [ChatCommand("home")]
        private void CmdHome(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "home"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (!_config.Home.Enabled)
                return;

            if (args.Length != 1)
            {
                SendChatMessage(player, "Syntax: /home {home}");
                return;
            }

            string home = args[0].ToLower();

            if (!GetHomes(player).Keys.Contains(home))
            {
                SendChatMessage(player, GetMessage("Unknown Home", player).Replace("{home}", home));
                return;
            }
            
            if (_config.Home.CooldownInMinutes > 0)
            {
                if (!_lastHome.ContainsKey(player.SteamId.m_SteamID))
                {
                    _lastHome[player.SteamId.m_SteamID] = DateTime.UtcNow;
                }
                else
                {
                    TimeSpan elapsedTime = DateTime.UtcNow.Subtract(_lastHome[player.SteamId.m_SteamID]);

                    if (elapsedTime.Minutes <= _config.Home.CooldownInMinutes)
                    {
                        float nextHome = _config.Home.CooldownInMinutes - elapsedTime.Minutes;

                        SendChatMessage(player, GetMessage("Home Cooldown", player).Replace("{time}", nextHome.ToString()));
                        return;
                    }

                    _lastHome[player.SteamId.m_SteamID] = DateTime.UtcNow;
                }
            }

            if (_config.Home.CheckForStakes && !HasStakeAuthority(player, _homes[player.SteamId.m_SteamID][home]))
            {
                SendChatMessage(player, GetMessage("Home Compromised", player));
                RemoveHome(player, home);
                return;
            }

            SendChatMessage(player,
                GetMessage("Teleporting Soon", player)
                    .Replace("{time}", _config.Home.TeleportTimer.ToString())
            );

            timer.Once(_config.Home.TeleportTimer, () =>
            {
                TeleportToLocation(player, _homes[player.SteamId.m_SteamID][home]);
                SendChatMessage(player, GetMessage("Home Teleported", player).Replace("{home}", home));
            });
        }

        [ChatCommand("homes")]
        private void CmdHomes(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "home"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (!_config.Home.Enabled)
                return;

            if (GetHomeCount(player) == 0)
            {
                SendChatMessage(player, GetMessage("No Homes", player));
                return;
            }

            SendChatMessage(player,
                GetMessage("Home List", player)
                    .Replace("{homes}", GetHomes(player).Keys.ToSentence())
            );
        }

        private int GetHomeLimit(PlayerSession player)
        {
            int limit = _config.Home.HomeCountLimit;

            foreach (var kvp in _config.Home.HomeLimitPermissions)
                if (kvp.Value > limit && HasPerm(player, kvp.Key))
                    limit = kvp.Value;

            return limit;
        }

        private Dictionary<string, Vector3> GetHomes(PlayerSession player)
        {
            if (!_homes.ContainsKey(player.SteamId.m_SteamID))
                _homes.Add(player.SteamId.m_SteamID, new Dictionary<string, Vector3>());

            return _homes[player.SteamId.m_SteamID];
        }

        private int GetHomeCount(PlayerSession player) => GetHomes(player).Count;

        private void AddHome(PlayerSession player, string name)
        {
            if (!_homes.ContainsKey(player.SteamId.m_SteamID))
                _homes.Add(player.SteamId.m_SteamID, new Dictionary<string, Vector3>());
            
            _homes[player.SteamId.m_SteamID].Add(name, player.WorldPlayerEntity.transform.position);

            SaveData();
        }

        private void RemoveHome(PlayerSession player, string name)
        {
            if (!_homes.ContainsKey(player.SteamId.m_SteamID))
                return;

            _homes[player.SteamId.m_SteamID].Remove(name);

            SaveData();
        }

        #endregion

        #region Warps

        [ChatCommand("removewarp")]
        private void CmdRemoveWarp(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "admin"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (args.Length != 1)
            {
                SendChatMessage(player, "Syntax: /removewarp {warp}");
                return;
            }

            string warp = args[0].ToLower();

            if (!_warps.ContainsKey(warp))
            {
                SendChatMessage(player, GetMessage("Unknown Warp", player).Replace("{warp}", warp));
                return;
            }

            RemoveWarp(warp);
            SendChatMessage(player, GetMessage("Warp Removed", player).Replace("{warp}", warp));
        }

        [ChatCommand("setwarp")]
        private void CmdSetWarp(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "admin"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (args.Length != 1)
            {
                SendChatMessage(player, "Syntax: /setwarp {warp}");
                return;
            }

            string warp = args[0].ToLower();

            if (_warps.ContainsKey(warp))
            {
                SendChatMessage(player, GetMessage("Warp Exists", player).Replace("{warp}", warp));
                return;
            }

            CreateWarp(player, warp);
            SendChatMessage(player, GetMessage("Warp Set", player).Replace("{warp}", warp));
        }

        [ChatCommand("warp")]
        private void CmdWarp(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "warp"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (!_config.Warp.Enabled)
                return;

            if (args.Length != 1)
            {
                SendChatMessage(player, "Syntax: /warp {warp}");
                return;
            }

            string warp = args[0].ToLower();

            if (!_warps.ContainsKey(warp))
            {
                SendChatMessage(player, GetMessage("Unknown Warp", player).Replace("{warp}", warp));
                return;
            }

            if (_config.Warp.CooldownInMinutes > 0 && _lastWarp.ContainsKey(player.SteamId.m_SteamID))
            {
                TimeSpan elapsedTime = DateTime.UtcNow.Subtract(_lastWarp[player.SteamId.m_SteamID]);

                if (elapsedTime.Minutes <= _config.Warp.CooldownInMinutes)
                {
                    float nextWarp = _config.Warp.CooldownInMinutes - elapsedTime.Minutes;

                    SendChatMessage(player,
                        GetMessage("Warp Cooldown", player)
                            .Replace("{time}", nextWarp.ToString())
                    );

                    return;
                }
            }

            _lastWarp[player.SteamId.m_SteamID] = DateTime.UtcNow;

            SendChatMessage(player, 
                GetMessage("Teleporting Soon", player)
                    .Replace("{time}", _config.Warp.TeleportTimer.ToString())
            );

            timer.Once(_config.Warp.TeleportTimer, () =>
            {
                TeleportToLocation(player, _warps[warp]);
                SendChatMessage(player, GetMessage("Warp Teleported", player).Replace("{warp}", warp));
            });
        }

        [ChatCommand("warps")]
        private void CmdWarps(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "warp"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (!_config.Warp.Enabled)
                return;

            if (_warps.Count == 0)
                SendChatMessage(player, GetMessage("No Warps", player));
            else
                SendChatMessage(player, GetMessage("Warp List", player).Replace("{warps}", _warps.Keys.ToSentence()));
        }

        private void CreateWarp(PlayerSession player, string name)
        {
            if (_warps.ContainsKey(name))
                return;

            _warps.Add(name, player.WorldPlayerEntity.transform.position);

            SaveData();
        }

        private void RemoveWarp(string name)
        {
            if (!_warps.ContainsKey(name))
                return;

            _warps.Remove(name);

            SaveData();
        }

        #endregion

        #region Teleport Requests

        [ChatCommand("tpr")]
        private void CmdTpr(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "tpr"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (!_config.TPR.Enabled)
                return;

            if (args.Length != 1)
            {
                SendChatMessage(player, "Syntax: /tpr {player}");
                return;
            }

            PlayerSession target = GetPlayer(args[0], player);
            if (target == null) return;

            if (target == player)
            {
                SendChatMessage(player, GetMessage("Teleport To Self", player));
                return;
            }

            if (_pendingRequests.ContainsValue(target) || _pendingRequests.ContainsKey(target))
            {
                SendChatMessage(player, GetMessage("Already Pending", player).Replace("{player}", target.Identity.Name));
                return;
            }

            if (_config.TPR.CooldownInMinutes > 0 && _lastTpr.ContainsKey(player.SteamId.m_SteamID))
            {
                TimeSpan elapsedTime = DateTime.UtcNow.Subtract(_lastTpr[player.SteamId.m_SteamID]);
                float nextTp = _config.TPR.CooldownInMinutes - elapsedTime.Minutes;

                if (elapsedTime.Minutes <= _config.TPR.CooldownInMinutes)
                {
                    SendChatMessage(player, GetMessage("TPR Cooldown", player).Replace("{time}", nextTp.ToString()));
                    return;
                }
            }

            SendRequest(player, target);
        }

        [ChatCommand("tpa")]
        private void CmdTpa(PlayerSession player, string command, string[] args)
        {
            if (!HasPerm(player, "tpr"))
            {
                SendChatMessage(player, GetMessage("No Permission", player));
                return;
            }

            if (!_config.TPR.Enabled)
                return;

            if (!_pendingRequests.ContainsValue(player))
            {
                SendChatMessage(player, GetMessage("No Pending", player));
                return;
            }

            PlayerSession source = _pendingRequests.Keys.FirstOrDefault(p => _pendingRequests[p] == player);

            if (source == null)
                return;

            if (!IsSafePlayer(player))
            {
                SendChatMessage(player, GetMessage("Target Location Unsafe", player));
                return;
            }

            if (_config.TPR.CooldownInMinutes > 0)
                _lastTpr[source.SteamId.m_SteamID] = DateTime.UtcNow;

            SendChatMessage(source, GetMessage("Accepted Request", player).Replace("{player}", player.Identity.Name));
            SendChatMessage(source, GetMessage("Teleporting Soon", source).Replace("{time}", _config.TPR.TeleportTimer.ToString()));

            if (_pendingTimers.ContainsKey(source))
                _pendingTimers[source].Destroy();

            if (_pendingRequests.ContainsKey(source))
                _pendingRequests.Remove(source);

            timer.Once(_config.TPR.TeleportTimer, () =>
            {
                SendChatMessage(source, GetMessage("Teleported", source).Replace("{target}", player.Identity.Name));
                TeleportToLocation(source, player.WorldPlayerEntity.gameObject.transform.position);

                if (_pendingTimers.ContainsKey(player))
                    _pendingTimers.Remove(player);
            });
        }

        private void SendRequest(PlayerSession player, PlayerSession target)
        {
            _pendingRequests[player] = target;

            SendChatMessage(player, GetMessage("Request Sent", player));
            SendChatMessage(target, GetMessage("Request Got", target).Replace("{player}", player.Identity.Name));

            _pendingTimers[player] = timer.Once(_config.TPR.PendingTimer, () =>
            {
                _pendingRequests.Remove(player);

                SendChatMessage(player, GetMessage("Request Ran Out", player));
                SendChatMessage(target, GetMessage("Request Ran Out", target));
            });
        }

        #endregion

        #region Helpers

        #region Teleportation

        private void TeleportToPlayer(PlayerSession player, PlayerSession target)
        {
            GameObject playerEntity = target.WorldPlayerEntity.gameObject;

            TeleportToLocation(player, playerEntity.transform.position);
        }

        private void TeleportToLocation(PlayerSession player, Vector3 location)
        {
            var hookResult = Interface.Call("CanTeleport", player, location);
            if (hookResult is bool && (bool) hookResult == false)
            {
                return;
            }

            GameObject playerEntity = player.WorldPlayerEntity.gameObject;

            playerEntity.transform.position = location;
        }

        #endregion

        #region Location Validation

        private bool HasStakeAuthority(PlayerSession player) => 
            HasStakeAuthority(player, player.WorldPlayerEntity.gameObject.transform.position);

        private bool HasStakeAuthority(PlayerSession player, Vector3 vector)
        {
            List<OwnershipStakeServer> entities = GetStakesInArea(vector, _config.Home.StakeRadius);
            return entities.Any(e => e.IsBuildAuthorized(player.Identity));
        }

        private List<OwnershipStakeServer> GetStakesInArea(Vector3 pos, float radius)
        {
            List<OwnershipStakeServer> entities = new List<OwnershipStakeServer>();

            foreach (OwnershipStakeServer entity in Resources.FindObjectsOfTypeAll<OwnershipStakeServer>())
            {
                if (Vector3.Distance(entity.transform.position, pos) <= radius)
                    entities.Add(entity);
            }

            return entities;
        }

        // Credit to Bankroll Tom
        public bool IsSafeLocation(Vector3 pos)
        {
            var playerCenterOffset = 1.1f; // offset from position to player center in Y-axis
            var crouchHalfHeight = .75f; // half the capsule height of crouching character
            var playerRadius = .36f;
            var capsuleBottom = pos + (playerCenterOffset - crouchHalfHeight + playerRadius) * Vector3.up;
            var capsuleTop = pos + (playerCenterOffset + crouchHalfHeight - playerRadius) * Vector3.up;

            if (Physics.CheckCapsule(capsuleBottom, capsuleTop, playerRadius, LayerMaskManager.TerrainConstructionsMachines, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return !PhysicsHelpers.IsInRock(pos + Vector3.up * playerCenterOffset);
        }

        // Credit to Bankroll Tom
        public bool IsSafePlayer(PlayerSession player)
        {
            GameObject playerEntity = player.WorldPlayerEntity.gameObject;

            // unsafe to teleport to a player in vehicle
            if (playerEntity.GetComponent<CharacterMotorSimple>().InsideVehicle != null)
            {
                return false;
            }

            return IsSafeLocation(playerEntity.transform.position);
        }

        #endregion

        private PlayerSession GetPlayer(string nameOrID, PlayerSession player)
        {
            ulong steamId;
            if (TryParseSteamId(nameOrID, out steamId))
            {
                PlayerSession session = GameManager.Instance.GetSessions().Values
                    .FirstOrDefault(p => p.SteamId.m_SteamID == steamId);

                if (session == null)
                    SendChatMessage(player, $"Could not find player with ID '{nameOrID}'");

                return session;
            }

            List<PlayerSession> foundPlayers = new List<PlayerSession>();

            foreach (PlayerSession session in GameManager.Instance.GetSessions().Values)
            {
                if (session.Identity.Name.ToLower() == nameOrID.ToLower())
                    return session;

                if (session.Identity.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(session);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    SendChatMessage(player, $"Could not find player with name '{nameOrID}'");
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = foundPlayers
                        .Select(p => p.Identity.Name)
                        .ToArray();

                    SendChatMessage(player, "Multiple matching players found: \n" + string.Join(", ", names));
                    break;
            }

            return null;
        }

        private bool TryParseSteamId(string id, out ulong result)
        {
            if (id.Length == 17 && id.StartsWith("7656119") && ulong.TryParse(id, out result))
            {
                return true;
            }

            result = 0;
            return false;
        }

        private bool HasPerm(PlayerSession session, string perm) => 
            permission.UserHasPermission(session.SteamId.ToString(), $"{Name}.{perm}");

        private string GetMessage(string key, PlayerSession session) => lang.GetMessage(key, this, session.SteamId.ToString());

        private void SendChatMessage(PlayerSession player, string message)
        {
            hurt.SendChatMessage(player, null, message);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Permission", "You don't have permission to use this command." },
                { "Request Ran Out", "Your pending teleport request ran out of time." },
                { "Request Sent", "Teleport request sent." },
                { "Request Got", "{player} would like to teleport to you. Accept by typing /tpa." },
                { "Teleported", "You have been teleported to {target}." },
                { "Accepted Request", "{player} has accepted your teleport request." },
                { "No Pending", "You don't have a pending teleport request." },
                { "Already Pending", "{player} already has a teleport request pending." },
                { "Teleporting Soon", "You will be teleported in {time} seconds." },
                { "Teleport To Self", "You may not teleport to yourself." },
                { "No Homes", "You do not have any homes." },
                { "Home Set", "You have set your home '{home}'" },
                { "Home Removed", "You have removed your home '{home}'" },
                { "Home Exists", "You already have a home called '{home}'" },
                { "Home Teleported", "You have been teleported to your home '{home}'" },
                { "Home List", "Your Homes: {homes}" },
                { "Max Homes", "You may not have more than {count} homes!" },
                { "Unknown Home", "You don't have a home called '{home}'" },
                { "Home Compromised", "You are not authorized at any stakes near your home '{home}'. The home was therefore removed." },
                { "No Stake", "You need to be close to a stake you're authorized at to set a home." },
                { "Home Cooldown", "You need to wait {time} minutes before teleporting to a home again." },
                { "TPR Cooldown", "You need to wait {time} minutes before sending the next teleport request." },
                { "Warp Set", "You have set warp '{warp}' at your current location." },
                { "Warp Removed", "You have removed warp '{warp}'" },
                { "Warp Teleported", "You have been teleported to warp '{warp}'" },
                { "Unknown Warp", "There is no warp called '{warp}'" },
                { "Warp List", "Available Warps: {warps}" },
                { "Warp Exists", "There already is a warp called '{warp}'" },
                { "No Warps", "There are no warps set." },
                { "Warp Cooldown", "You need to wait {time} minutes before teleporting to a warp again." },
                { "Target Location Unsafe", "The target location is currently not safe to teleport to." }
            }, this);
        }

        #endregion

        #region Configuration

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration
        {
            Home =
            {
                HomeLimitPermissions =
                {
                    [$"{this.Name}.homelimit.basic"] = 1,
                    [$"{this.Name}.homelimit.extended"] = 2
                }
            }
        };

        protected override void SaveConfig() => Config.WriteObject(_config);

        private class Configuration
        {
            public TPRConfig TPR { get; set; } = new TPRConfig();
            public HomeConfig Home { get; set; } = new HomeConfig();
            public WarpConfig Warp { get; set; } = new WarpConfig();

            public class TPRConfig
            {
                public bool Enabled { get; set; } = true;

                [JsonProperty("Pending Timer (in seconds)")]
                public float PendingTimer { get; set; } = 30f;

                [JsonProperty("Teleport Timer (in seconds)")]
                public float TeleportTimer { get; set; } = 15f;

                [JsonProperty("Cooldown (in minutes)")]
                public float CooldownInMinutes { get; set; } = 5f;
            }

            public class HomeConfig
            {
                public bool Enabled { get; set; } = true;

                [JsonProperty("Check for Stakes (true/false)")]
                public bool CheckForStakes { get; set; } = true;

                [JsonProperty("Stake Detection Radius")]
                public float StakeRadius { get; set; } = 10f;

                [JsonProperty("Teleport Timer (in seconds)")]
                public float TeleportTimer { get; set; } = 15f;

                [JsonProperty("Cooldown (in minutes)")]
                public float CooldownInMinutes { get; set; } = 5f;

                [JsonProperty("Home Count Limit")]
                public int HomeCountLimit { get; set; } = 3;

                [JsonProperty("Home Count Limits (granted by permissions)")]
                public Dictionary<string, int> HomeLimitPermissions { get; set; } = new Dictionary<string, int>();
            }

            public class WarpConfig
            {
                public bool Enabled { get; set; } = true;

                [JsonProperty("Teleport Timer (in seconds)")]
                public float TeleportTimer { get; set; } = 15f;

                [JsonProperty("Cooldown (in minutes)")]
                public float CooldownInMinutes { get; set; } = 10f;
            }
        }

        #endregion
    }
}
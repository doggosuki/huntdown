using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using static Huntdown.ConfigSettings;
using static Huntdown.Huntdown;

namespace Huntdown
{
    public class Mission
    {
        private readonly string _name;
        private readonly Dictionary<EnemyKey, int> _enemiesToSpawn;
        private static List<EnemyAI> _aliveEnemies = new List<EnemyAI>();
        public static int _maxEnemies;
        private RewardPool _rewardPool;
        private readonly int _baseWeight;
        private int _weight;
        private static int _totalWeight;
        private static readonly int _missionChance = (int)ConfigEntries[(int)ConfigIndexes.GeneralPercentageChance].BoxedValue;
        private bool _enabled;

        private readonly List<EnemyKey> _randomEnemyChoices = new List<EnemyKey>
        {
            EnemyKey.Thumper,
            EnemyKey.BunkerSpider,
            EnemyKey.HoarderBug,
            EnemyKey.SnareFlea,
            EnemyKey.Nutcracker,
            EnemyKey.Masked,
            EnemyKey.Bracken,
            EnemyKey.Maneater
        };

        public Mission(string name, int baseWeight, Dictionary<EnemyKey, int> enemiesToSpawn, bool enabled, RewardPool rewardPool)
        {
            _name = name;
            _baseWeight = baseWeight;
            _weight = baseWeight;
            _enemiesToSpawn = enemiesToSpawn;
            _enabled = enabled;
            _rewardPool = rewardPool;
        }

        public void Start(ref SelectableLevel level, EnemyVent[] allVents)
        {
            _currentLevel = level;
            _allEnemyVents = allVents;
            SpawnEnemiesAtVent(ref level, RollRandomFarVent(allVents));
        }

        public void End()
        {
            if (_currentMission != null)
            {
                _aliveEnemies.Clear();
                _currentMission = null;
            }
        }

        public static EnemyVent RollRandomFarVent(EnemyVent[] allVents)
        {
            _logger.LogInfo("Rolling for a random faraway vent...");

            Vector3 entrancePos = RoundManager.FindMainEntrancePosition();
            float longestDist = 0f;
            List<EnemyVent> allFarVents = new List<EnemyVent>();


            _logger.LogInfo("MainEntrancePos: " + entrancePos);


            for (int i = 0; i < allVents.Length; i++)
            {
                _logger.LogInfo(i + "VentPosEntrancePos: " + allVents[i].transform.position);
                float dist = Vector3.Distance(entrancePos, allVents[i].transform.position);
                if (dist > longestDist)
                {
                    longestDist = dist;
                }
            }

            _logger.LogInfo("longest dist: " + longestDist);

            // Find the midpoint between the entrance and the furthest vent
            Vector3 midpoint = (entrancePos + allVents.Where(vent => Vector3.Distance(entrancePos, vent.transform.position) == longestDist).FirstOrDefault().transform.position) / 2f;

            _logger.LogInfo("midpoint: " + midpoint);

            // Calculate the halfway distance from the entrance to the midpoint
            float halfWayDist = Vector3.Distance(entrancePos, midpoint);

            _logger.LogInfo("halfway dist: " + halfWayDist);

            // Filter vents that are at least halfway from the entrance
            for (int i = 0; i < allVents.Length; i++)
            {
                _logger.LogInfo(i + "VentPosEntrancePos: " + allVents[i].transform.position);
                float dist = Vector3.Distance(entrancePos, allVents[i].transform.position);
                if (dist >= halfWayDist)
                {
                    _logger.LogInfo("dist: " + dist);
                    _logger.LogInfo("halfwaydist " + halfWayDist);
                    allFarVents.Add(allVents[i]);
                }
            }

            var rand = new System.Random();
            int roll = rand.Next(0, allFarVents.Count);
            _logger.LogInfo("Rolled " + roll + " out of " + allFarVents.Count + ".");
            return allFarVents[roll];
        }

        public static bool RollMissionChance()
        {
            _logger.LogInfo("Rolling for mission generation...");
            var rand = new System.Random();
            int roll = rand.Next(0, 100);
            _logger.LogInfo("Rolled " + roll + " out of " + _missionChance + ".");
            _logger.LogInfo("Mission received: " + (roll <= _missionChance) + ".");
            return (roll <= _missionChance);
        }

        private void ForceAIInside(EnemyAI enemyAI)
        {
            _logger.LogInfo("Forcing AI to work indoors.");
            enemyAI.enemyType.isOutsideEnemy = false;
            enemyAI.daytimeEnemyLeaving = false;
            enemyAI.allAINodes = GameObject.FindGameObjectsWithTag("AINode");
        }

        private void SpawnUnnaturalEnemy(ref SelectableLevel level, EnemyVent vent, EnemyKey enemyKey)
        {
            _logger.LogInfo($"Spawning a single unnatural enemy of type: {_storedEnemies[(int)enemyKey].Name}");

            level.Enemies.Add(_storedEnemies[(int)enemyKey].SpawnableEnemy);
            int enemyIndex = level.Enemies.IndexOf(_storedEnemies[(int)enemyKey].SpawnableEnemy);

            RoundManager.Instance.SpawnEnemyOnServer(vent.transform.position, 0f, enemyIndex);
            EnemyAI spawnedEnemy = RoundManager.Instance.SpawnedEnemies.Last();
            _aliveEnemies.Add(spawnedEnemy);

            if (spawnedEnemy is MouthDogAI || spawnedEnemy is BaboonBirdAI || spawnedEnemy is ForestGiantAI)
            {
                ForceAIInside(spawnedEnemy);
            }
            ApplyConditionalModifications(spawnedEnemy);

            level.Enemies.RemoveAt(enemyIndex); // Remove immediately after spawning this one.
        }


        private void ApplyConditionalModifications(EnemyAI enemyAI)
        {
            // Apply ScanNode modification
            ScanNodeProperties scanNode = enemyAI.gameObject.GetComponentInChildren<ScanNodeProperties>();
            if (scanNode != null)
            {
                if ((bool)ConfigEntries[(int)ConfigIndexes.ChangeSubtext].BoxedValue)
                {
                    scanNode.subText = "<color=white>TARGET</color>";
                }
                else
                {
                    scanNode.headerText = "<color=white>TARGET</color>";
                }
            }

            if (_currentMission == null) return; // Early exit if no mission

            // Facility Keeper
            if (_currentMission.Name == "Facility Keeper")
            {
                SafeDespawn(enemyAI);
                enemyAI.transform.localScale /= 4;
                enemyAI.enemyHP = 5;
                SafeRespawn(enemyAI);
            }
            // Giant Size: Upgraded
            else if (_currentMission.Name == "Giant Size: Upgraded")
            {
                SafeDespawn(enemyAI);
                enemyAI.transform.localScale *= 2;
                if (enemyAI is HoarderBugAI) enemyAI.enemyHP *= 12;
                else if (enemyAI is CentipedeAI) enemyAI.enemyHP *= 17;
                else if (enemyAI is MaskedPlayerEnemy) enemyAI.enemyHP *= 5;
                else if (enemyAI is CaveDwellerAI) enemyAI.enemyHP *= 2; //Maneater
                else enemyAI.enemyHP *= 3;
                SafeRespawn(enemyAI);
            }
            // Big Trouble Little Enemies & Who let the puppies out?
            else if (_currentMission.Name == "Big Trouble Little Enemies" || _currentMission.Name == "Who let the puppies out?")
            {
                SafeDespawn(enemyAI);
                enemyAI.transform.localScale /= 3;
                enemyAI.enemyHP = 1;
                SafeRespawn(enemyAI);
            }
        }


        private void SafeDespawn(EnemyAI enemyAI)
        {
            if (enemyAI.gameObject.GetComponent<NetworkObject>().IsSpawned)
            {
                enemyAI.gameObject.GetComponent<NetworkObject>().Despawn(false);
            }
        }

        private void SafeRespawn(EnemyAI enemyAI)
        {
            if (!enemyAI.gameObject.GetComponent<NetworkObject>().IsSpawned)
            {
                enemyAI.gameObject.GetComponent<NetworkObject>().Spawn();
            }
        }


        public void SpawnEnemiesAtVent(ref SelectableLevel level, EnemyVent vent)
        {
            _logger.LogInfo("Spawning mission targets...");
            _aliveEnemies.Clear();

            foreach (KeyValuePair<EnemyKey, int> entry in _enemiesToSpawn)
            {
                for (int i = 0; i < entry.Value; i++) // Loop for EACH enemy to be spawned.
                {
                    EnemyKey enemyKeyToSpawn = entry.Key;

                    if (enemyKeyToSpawn == EnemyKey.Random)
                    {
                        // Pick a random enemy *every time* we spawn an enemy.
                        var rand = new System.Random();
                        int randomEnemyIndex = rand.Next(0, _randomEnemyChoices.Count);
                        enemyKeyToSpawn = _randomEnemyChoices[randomEnemyIndex];
                        _logger.LogInfo($"Randomly selected enemy: {_storedEnemies[(int)enemyKeyToSpawn].Name}");
                    }

                    // Check if the enemy is natural and spawn accordingly.
                    if (!level.Enemies.Contains(_storedEnemies[(int)enemyKeyToSpawn].SpawnableEnemy))
                    {
                        SpawnUnnaturalEnemy(ref level, vent, enemyKeyToSpawn); // Spawn a *single* unnatural enemy.
                    }
                    else
                    {
                        // Natural enemy spawning - simplified. Spawn a *single* natural enemy.
                        RoundManager.Instance.SpawnEnemyOnServer(vent.transform.position, 0f, level.Enemies.IndexOf(_storedEnemies[(int)enemyKeyToSpawn].SpawnableEnemy));
                        EnemyAI spawnedEnemy = RoundManager.Instance.SpawnedEnemies.Last();
                        _aliveEnemies.Add(spawnedEnemy);
                        ApplyConditionalModifications(spawnedEnemy); // Apply modifications.
                    }
                }
            }

            _maxEnemies = _aliveEnemies.Count;
            _logger.LogInfo($"Total enemies spawned: {_maxEnemies}");
        }

        public string Name
        {
            get { return _name; }
        }

        public Dictionary<EnemyKey, int> EnemiesToSpawn
        {
            get { return _enemiesToSpawn; }
        }

        public RewardPool RewardPool
        {
            get { return _rewardPool; }
            set { _rewardPool = value; }
        }

        public int BaseWeight
        {
            get { return _baseWeight; }
        }

        public int Weight
        {
            get { return _weight; }
            set { _weight = value; }
        }

        public static int TotalWeight
        {
            get { return _totalWeight; }
            set { _totalWeight = value; }
        }

        public static int MissionChance
        {
            get { return _missionChance; }
        }

        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public static List<EnemyAI> AliveEnemies
        {
            get { return _aliveEnemies; }
            set { _aliveEnemies = value; }
        }
    }
}
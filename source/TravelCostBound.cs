using System;
using System.Collections;
using System.Reflection;
using HungryPathing.Planning;
using Timberborn.BlueprintSystem;
using Timberborn.TemplateSystem;
using Timberborn.ZiplineSystem;
using UnityEngine;

namespace HungryPathing
{
    // The least a walk can cost per tile of straight line in this game, which makes straight-line time a lower bound
    // on the walker's travel time once it is scaled by it (FuelPlanner.StraightLineRulesOut). Ground and paths cost
    // one per tile, but buildings add cheaper edges to the navigation mesh: tubeways 0.25 per tile, stair and slope
    // climbs 0.4 per level, zipline cables 0.4 per tile of cable. The value is read from the building templates this
    // game loaded, once per game, the first time a planner asks: 0.25 where tubeways can be built, 0.4 with stairs or
    // ziplines, 1 with none of them. Every player in a co-op game loads the same faction, game version and mods, so
    // every player reads the same number; nothing here depends on the map, the UI, the machine or when it is read.
    public class TravelCostBound
    {
        // The cheapest edge in the base game, a tubeway tile, in case the templates cannot be read.
        private const float Fallback = 0.25f;

        // Internal to the game, so it is found by name; the properties read below are the blueprint fields.
        private const string NavMeshSettingsSpecName = "Timberborn.BlockSystemNavigation.BlockObjectNavMeshSettingsSpec";

        private readonly TemplateService _templateService;
        private readonly ISpecService _specService;
        private float _minCostPerUnit = float.NaN;

        public TravelCostBound(TemplateService templateService, ISpecService specService)
        {
            _templateService = templateService;
            _specService = specService;
        }

        public float MinCostPerUnit
        {
            get
            {
                if (float.IsNaN(_minCostPerUnit))
                {
                    _minCostPerUnit = Read();
                }
                return _minCostPerUnit;
            }
        }

        private float Read()
        {
            try
            {
                float cheapest = FuelPlanner.GroundCostPerUnit;
                int buildings = 0;
                foreach (ComponentSpec spec in _templateService.GetAll<ComponentSpec>())
                {
                    if (spec.GetType().FullName == NavMeshSettingsSpecName)
                    {
                        cheapest = FoldEdgeGroups(cheapest, spec);
                        buildings++;
                    }
                }
                // A cable costs CableUnitCost per tile of its length. It is counted whether or not this faction
                // builds ziplines; every faction in the base game has stairs at the same 0.4 anyway.
                foreach (ZiplineCableNavMeshSpec cable in _specService.GetSpecs<ZiplineCableNavMeshSpec>())
                {
                    cheapest = FuelPlanner.CheapestCostPerUnit(cheapest, cable.CableUnitCost, 1f);
                }
                Log.Info($"Cheapest travel here: {cheapest:0.###} per tile of straight line (path costs of " +
                         $"{buildings} buildings and zipline cables), so storages up to {1f / cheapest:0.#} times " +
                         "the near-food limit away in a straight line are still measured.");
                return cheapest;
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not read path costs from the building templates; assuming the base game's " +
                            $"cheapest, {Fallback} per tile. {exception.GetType().Name}: {exception.Message}");
                return Fallback;
            }
        }

        private static float FoldEdgeGroups(float cheapest, object settingsSpec)
        {
            foreach (object group in (IEnumerable)Property(settingsSpec, "EdgeGroups"))
            {
                float cost = (float)Property(group, "Cost");
                foreach (object edge in (IEnumerable)Property(group, "AddedEdges"))
                {
                    Vector3Int start = (Vector3Int)Property(edge, "Start");
                    Vector3Int end = (Vector3Int)Property(edge, "End");
                    cheapest = FuelPlanner.CheapestCostPerUnit(cheapest, cost, Vector3Int.Distance(start, end));
                }
            }
            return cheapest;
        }

        private static object Property(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(instance.GetType().FullName, name);
            }
            return property.GetValue(instance);
        }
    }
}

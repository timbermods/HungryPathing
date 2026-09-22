using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Timberborn.BaseComponentSystem;
using Timberborn.Effects;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSpecs;

namespace HungryPathing
{
    // Lives on each district center next to the game's DistrictNeedBehaviorService and mirrors what that service
    // is told: which storages (need behaviors) currently hold which consumable good group. The mirror exists so the
    // mod can rank storages its own way without reaching into the service's private state. Lists are kept in
    // insertion order and iterated in that order, so every machine sees the same sequence.
    public class HungryPathingDistrictIndex : BaseComponent
    {
        internal sealed class Group
        {
            public string Key;
            public ImmutableArray<string> NeedIds;
            public ImmutableArray<InstantEffect> Effects;
            public readonly List<NeedBehavior> Behaviors = new List<NeedBehavior>();
        }

        private static readonly IReadOnlyList<Group> NoGroups = new List<Group>();

        private readonly List<Group> _groups = new List<Group>();
        private readonly Dictionary<string, Group> _groupsByKey = new Dictionary<string, Group>();
        private readonly Dictionary<string, List<Group>> _groupsByNeed = new Dictionary<string, List<Group>>();

        public void Add(IReadOnlyList<InstantEffectSpec> effects, NeedBehavior needBehavior)
        {
            Group group = GetOrCreate(effects);
            if (!group.Behaviors.Contains(needBehavior))
            {
                group.Behaviors.Add(needBehavior);
            }
        }

        public void Remove(IReadOnlyList<InstantEffectSpec> effects, NeedBehavior needBehavior)
        {
            if (_groupsByKey.TryGetValue(Key(effects), out Group group))
            {
                group.Behaviors.Remove(needBehavior);
            }
        }

        internal IReadOnlyList<Group> GroupsFor(string needId)
        {
            return _groupsByNeed.TryGetValue(needId, out List<Group> groups) ? groups : NoGroups;
        }

        private Group GetOrCreate(IReadOnlyList<InstantEffectSpec> effects)
        {
            string key = Key(effects);
            if (_groupsByKey.TryGetValue(key, out Group existing))
            {
                return existing;
            }
            ImmutableArray<string>.Builder needIds = ImmutableArray.CreateBuilder<string>(effects.Count);
            ImmutableArray<InstantEffect>.Builder instantEffects = ImmutableArray.CreateBuilder<InstantEffect>(effects.Count);
            for (int i = 0; i < effects.Count; i++)
            {
                needIds.Add(effects[i].NeedId);
                instantEffects.Add(InstantEffect.FromSpec(effects[i], 1));
            }
            Group group = new Group
            {
                Key = key,
                NeedIds = needIds.MoveToImmutable(),
                Effects = instantEffects.MoveToImmutable()
            };
            _groups.Add(group);
            _groupsByKey[key] = group;
            foreach (string needId in group.NeedIds)
            {
                if (!_groupsByNeed.TryGetValue(needId, out List<Group> list))
                {
                    list = new List<Group>();
                    _groupsByNeed[needId] = list;
                }
                if (!list.Contains(group))
                {
                    list.Add(group);
                }
            }
            return group;
        }

        private static string Key(IReadOnlyList<InstantEffectSpec> effects)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < effects.Count; i++)
            {
                builder.Append(effects[i].NeedId).Append(':')
                    .Append(effects[i].Points.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }
            return builder.ToString();
        }
    }
}

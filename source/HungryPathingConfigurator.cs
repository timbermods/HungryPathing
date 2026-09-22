using Bindito.Core;
using Timberborn.Beavers;
using Timberborn.GameDistricts;
using Timberborn.TemplateInstantiation;

namespace HungryPathing
{
    // Puts the two components on the entities that need them: the planner on every adult beaver, the storage
    // index on every district center. Both are plain components with no saved state. Runs once per game load on
    // every player, which makes it the one place where per-game static state is reset.
    [Context("Game")]
    public class HungryPathingConfigurator : IConfigurator
    {
        public void Configure(IContainerDefinition containerDefinition)
        {
            Stats.Reset();
            Safety.NewGame();
            containerDefinition.Bind<HungryPathingRootBehavior>().AsTransient();
            containerDefinition.Bind<HungryPathingDistrictIndex>().AsTransient();
            containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
        }

        private static TemplateModule ProvideTemplateModule()
        {
            TemplateModule.Builder builder = new TemplateModule.Builder();
            builder.AddDecorator<AdultSpec, HungryPathingRootBehavior>();
            builder.AddDecorator<DistrictCenter, HungryPathingDistrictIndex>();
            return builder.Build();
        }
    }
}

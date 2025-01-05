// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets
{
    public class RulesetInfo : IRulesetInfo
    {
        public string ShortName { get; set; } = string.Empty;

        public int OnlineID { get; set; } = -1;

        public string Name { get; set; } = string.Empty;

        public string InstantiationInfo { get; set; } = string.Empty;

        public RulesetInfo()
        {
        }

        public bool Available { get; set; }

        public Ruleset CreateInstance()
        {
            if (!Available)
                throw new RulesetLoadException(@"Ruleset not available");

            var type = Type.GetType(InstantiationInfo);

            if (type == null)
                throw new RulesetLoadException(@"Type lookup failure");

            var ruleset = Activator.CreateInstance(type) as Ruleset;

            if (ruleset == null)
                throw new RulesetLoadException(@"Instantiation failure");

            // overwrite the pre-populated RulesetInfo with a potentially database attached copy.
            // TODO: figure if we still want/need this after switching to realm.
            // ruleset.RulesetInfo = this;

            return ruleset;
        }
    }
}

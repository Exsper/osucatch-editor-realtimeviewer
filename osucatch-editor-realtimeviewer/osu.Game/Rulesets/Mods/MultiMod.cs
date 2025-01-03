// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mods
{
    public sealed class MultiMod : Mod
    {
        public override string Name => string.Empty;
        public override string Acronym => string.Empty;

        public Mod[] Mods { get; }

        public MultiMod(params Mod[] mods)
        {
            Mods = mods;
        }

        public override Type[] IncompatibleMods => Mods.SelectMany(m => m.IncompatibleMods).ToArray();
    }
}

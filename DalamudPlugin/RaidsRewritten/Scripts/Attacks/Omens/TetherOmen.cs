using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class TetherOmen
{
    public enum TetherVfx
    {
        ActivatedClose,
        ActivatedFar,
        DelayedClose,
        DelayedFar,
    }

    public static readonly Dictionary<TetherVfx, string> TetherVfxes = new()
    {
        {TetherVfx.ActivatedClose, "vfx/channeling/eff/chn_alpha0h.avfx"},
        {TetherVfx.ActivatedFar, "vfx/channeling/eff/chn_beta0h.avfx"},
        {TetherVfx.DelayedClose, "vfx/channeling/eff/chn_m0771_alpha0c.avfx"},
        {TetherVfx.DelayedFar, "vfx/channeling/eff/chn_m0771_beta0c.avfx"},
    };
}

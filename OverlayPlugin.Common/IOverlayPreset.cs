using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RainbowMage.OverlayPlugin
{
    public interface IOverlayPreset
    {
        string Name { get; }
        string Type { get; }
        string Url { get; }
        int[] Size { get; }
        bool Locked { get; }
        bool IsOverlayPreset { get; }
        bool IsSharedPreset { get; }
        List<string> Supports { get; }
    }
}


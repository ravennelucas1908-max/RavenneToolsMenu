using System.Runtime.InteropServices;

namespace RavenneToolsMenu.Core;

/// <summary>
/// Injection clavier/souris via SendInput.
/// Les clics souris ré-injectés portent un marqueur (sentinelle) pour que notre
/// propre hook global les ignore et évite toute boucle.
/// </summary>
internal static class InputSimulator
{
    // Tables de noms → codes virtuels pour les raccourcis du genre "ctrl+shift+t".
    private static readonly Dictionary<string, ushort> KeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = 0x11, ["control"] = 0x11,
        ["shift"] = 0x10, ["maj"] = 0x10,
        ["alt"] = 0x12, ["menu"] = 0x12, ["altgr"] = 0x12,
        ["win"] = 0x5B, ["windows"] = 0x5B, ["super"] = 0x5B,
        ["enter"] = 0x0D, ["return"] = 0x0D,
        ["esc"] = 0x1B, ["escape"] = 0x1B,
        ["tab"] = 0x09, ["space"] = 0x20,
        ["del"] = 0x2E, ["delete"] = 0x2E, ["suppr"] = 0x2E,
        ["backspace"] = 0x08, ["insert"] = 0x2D,
        ["home"] = 0x24, ["end"] = 0x23, ["pageup"] = 0x21, ["pagedown"] = 0x22,
        ["left"] = 0x25, ["up"] = 0x26, ["right"] = 0x27, ["down"] = 0x28,
        ["printscreen"] = 0x2C, ["prtsc"] = 0x2C,
        ["plus"] = 0xBB, ["minus"] = 0xBD, ["comma"] = 0xBC, ["period"] = 0xBE,
        ["f1"] = 0x70, ["f2"] = 0x71, ["f3"] = 0x72, ["f4"] = 0x73, ["f5"] = 0x74,
        ["f6"] = 0x75, ["f7"] = 0x76, ["f8"] = 0x77, ["f9"] = 0x78, ["f10"] = 0x79,
        ["f11"] = 0x7A, ["f12"] = 0x7B,
    };

    private static readonly ushort[] Modifiers = { 0x11, 0x10, 0x12, 0x5B, 0x5C };

    public static bool TryParseKeys(string combo, out List<ushort> keys)
    {
        keys = new List<ushort>();
        foreach (var tokenRaw in combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = tokenRaw;
            if (token.Length == 1)
            {
                char c = token[0];
                if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
                {
                    keys.Add((ushort)char.ToUpperInvariant(c));
                    continue;
                }
            }
            if (KeyNames.TryGetValue(token, out var vk)) keys.Add(vk);
            else return false;
        }
        return keys.Count > 0;
    }

    public static void PressKeys(List<ushort> keys)
    {
        // Ordre d'appui : modificateurs (ctrl, shift, alt, win) d'abord, touche finale ensuite,
        // puis relâchement en sens inverse. Un mauvais ordre enverrait la lettre seule.
        var ordered = keys
            .OrderBy(k =>
            {
                int i = Array.IndexOf(Modifiers, k);
                return i < 0 ? int.MaxValue : i;
            })
            .ToList();
        var inputs = new List<Win32.INPUT>(ordered.Count * 2);
        foreach (var vk in ordered) inputs.Add(Key(vk, false));
        for (int i = ordered.Count - 1; i >= 0; i--) inputs.Add(Key(ordered[i], true));
        Send(inputs);
    }

    public static void TapKey(ushort vk)
    {
        var inputs = new List<Win32.INPUT>(2) { Key(vk, false), Key(vk, true) };
        Send(inputs);
    }

    /// <summary>Clic "bouton du milieu" synthétique (ignoré par notre propre hook).</summary>
    public static void SynthesizeMiddleClick()
    {
        var inputs = new List<Win32.INPUT>(2)
        {
            Mouse(Win32.MOUSEEVENTF_MIDDLEDOWN),
            Mouse(Win32.MOUSEEVENTF_MIDDLEUP),
        };
        Send(inputs);
    }

    public static void SynthesizeX2Click()
    {
        var inputs = new List<Win32.INPUT>(2)
        {
            Mouse(Win32.MOUSEEVENTF_XDOWN, 0x0002_0000),
            Mouse(Win32.MOUSEEVENTF_XUP, 0x0002_0000),
        };
        Send(inputs);
    }

    private static Win32.INPUT Key(ushort vk, bool up) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        ki = new Win32.KEYBDINPUT { wVk = vk, dwFlags = up ? Win32.KEYEVENTF_KEYUP : 0 }
    };

    private static Win32.INPUT Mouse(uint flags, uint data = 0) => new()
    {
        type = Win32.INPUT_MOUSE,
        mi = new Win32.MOUSEINPUT { dwFlags = flags, mouseData = data, dwExtraInfo = (nuint)Win32.Sentinel }
    };

    private static void Send(List<Win32.INPUT> inputs)
    {
        var arr = inputs.ToArray();
        Win32.SendInput((uint)arr.Length, arr, Marshal.SizeOf<Win32.INPUT>());
    }
}

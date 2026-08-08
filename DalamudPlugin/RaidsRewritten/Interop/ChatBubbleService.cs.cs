// Adapted from https://github.com/NetStyx/ARealmRepopulated/blob/main/ARealmRepopulated/Core/Services/Chat/ChatBubbleService.cs
// 890ae94
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace RaidsRewritten.Interop;

public unsafe class ChatBubbleService {
    public void Talk(Character* character, string text, float duration) {
        using var newText = *Utf8String.FromString(text);
        character->YellBalloon.OpenBalloon(newText, duration, true, 0, false, false, true, 0);
    }

}

/* 
/// <summary>
/// Client::UI::Agent::AgentScreenLog.OpenBalloon()
/// </summary>
[Signature("E8 ?? ?? ?? ?? F6 86 ?? ?? ?? ?? ?? C7 46", DetourName = nameof(OpenBubbleDetour))]
private readonly Hook<OpenBubbleDelegate> _openBalloonHook = null!;
private delegate void OpenBubbleDelegate(AgentScreenLog* screenLog, Character* character, CStringPointer text, bool unk, int attachmentPoint);

/// <summary>
/// Definition for the minitalk call
/// </summary>
/// <remarks>
/// miniTalkController = <AgentScreenLog + 0x3e8>
/// miniTalkDisplayer = <miniTalkController + 0x10>
/// miniTalkDisplayTypeTable = <miniTalkDisplayer + 0xa4>
/// miniTalkDisplayTypeTableEntrySize = 0x10
/// 
/// Client::UI::Misc::RaptureLogModule.ShowMiniTalkPlayer()
///     MaybeDisplayChatBubble(miniTalkController,&miniTalkArgs)
///         ForceDisplayChatBubble(miniTalkDisplayer, 0 , miniTalkArgs, miniTalkDisplayTypeTable + chatDisplayKind* miniTalkDisplayTypeTableEntrySize)
/// </remarks>

[Signature("48 89 74 24 ?? 57 48 83 EC ?? 48 8B 71 ?? 48 8B FA 48 85 F6 0F 84 ?? ?? ?? ?? 48 8B 42")]
private delegate* unmanaged<IntPtr, MiniTalkArgs, void> _showMiniTalkBubbles = null!;

[Signature("40 55 53 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? FF 41")]
private delegate* unmanaged<IntPtr, IntPtr, MiniTalkArgs, IntPtr, void> _forceShowMiniTalkBubbles = null!;

private const int _offsetMtController = 0x3e8;
private const int _offsetMtDisplayer = 0x10;
private const int _offsetMtDisplayTypes = 0xa4;
private const int _sizeMtDisplayType = 0x10;

var _miniTalkController = IntPtr.Add(new IntPtr(agentScreenLog), _offsetMtController);
if (_miniTalkController == IntPtr.Zero)
    return;

var _miniTalkDisplayer = Marshal.ReadIntPtr(_miniTalkController, _offsetMtDisplayer);
if (_miniTalkDisplayer == IntPtr.Zero)
    return;

var _miniTalkDisplayTypeTable = IntPtr.Add(_miniTalkDisplayer, _offsetMtDisplayTypes);
if (_miniTalkDisplayTypeTable == IntPtr.Zero)
    return;

var newString = Utf8String.CreateEmpty();
newString->SetString(text);

var miniTalkArgs = new MiniTalkArgs
{
    MiniTalkKind = 1,
    Unk2 = 0,
    StyleOrOpt = uint.MaxValue,
    Message = newString->StringPtr,
    BattleChar = (BattleChara*)gameObject
};

[StructLayout(LayoutKind.Explicit, Size = 0x20)]
public unsafe struct MiniTalkArgs
{
    /// <summary>
    /// 1..18 / Say, Yell, Shout ... and so on 
    /// </summary>
    [FieldOffset(0x0)]
    public byte MiniTalkKind;

    [FieldOffset(0x8)]
    public BattleChara* BattleChar;

    [FieldOffset(0x10)]
    public CStringPointer Message;

    [FieldOffset(0x18)]
    public byte Unk2;

    [FieldOffset(0x1c)]
    public uint StyleOrOpt;
}
*/

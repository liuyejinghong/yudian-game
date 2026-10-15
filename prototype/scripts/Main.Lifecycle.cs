#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
namespace Yudian;

public partial class Main
{
    private bool TutorialEnabled => _playerMode && DevelopmentEnabled && !LegacyAuthorityTest;
    private bool LegacyAuthorityTest => new[] { "YUDIAN_PLAYER_SELF_TEST", "YUDIAN_BOOTSTRAP_SELF_TEST", "YUDIAN_DEVELOPMENT_SELF_TEST" }
        .Any(name => System.Environment.GetEnvironmentVariable(name) == "1");
    private bool TutorialTest => System.Environment.GetEnvironmentVariable("YUDIAN_TUTORIAL_SELF_TEST") == "1";
    private bool PrivatePlayerTest => TutorialTest || System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST") == "1" || LegacyAuthorityTest;
    private bool _menuPaused;
    private string _lifecyclePage = "game", _lifecycleBackPage = "game", _pendingLifecycleAction = "";
    private readonly List<string> _menuStack = new();
    private int _activeSlot = 1, _selectedSlot = 1;
    private long _worldEpoch, _intentVersion, _worldTransitionRevision;
    private PlayerSlotView[] _slotViews = [];
    private PlayerSave? _initialPlayer;
    private string _transitionKind = "load";
    private int _transitionSlot;
    private LifecycleRollback? _lifecycleRollback;
    private sealed record LifecycleRollback(bool EntryOpen, bool MenuPaused, string[] Stack, string PendingAction,
        int ActiveSlot, int SelectedSlot, long WorldEpoch, string Notice);

    private string TutorialSaveRoot
    {
        get
        {
            if (!PrivatePlayerTest) return ProjectSettings.GlobalizePath("user://saves");
            string? root = System.Environment.GetEnvironmentVariable("YUDIAN_TUTORIAL_TEST_ROOT");
            if (string.IsNullOrWhiteSpace(root))
            {
                string? explicitSave = System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_TEST_SAVE");
                if (string.IsNullOrWhiteSpace(explicitSave) || !Path.IsPathFullyQualified(explicitSave))
                    throw new InvalidOperationException("测试必须同时提供私有槽根或绝对 TEST_SAVE 路径");
                root = Path.GetDirectoryName(explicitSave);
            }
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) || root.StartsWith("user://", StringComparison.Ordinal))
                throw new InvalidOperationException("测试槽根必须为明确的私有绝对路径");
            root = Path.GetFullPath(root);
            string formal = Path.GetFullPath(ProjectSettings.GlobalizePath("user://saves"));
            if (root == formal || root.StartsWith(formal + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("测试不得使用正式存档目录");
            return root;
        }
    }
    private string TutorialSlotPath(int slot)
    {
        if (slot is < 1 or > 3) throw new ArgumentException("请选择槽位1–3");
        return Path.Combine(TutorialSaveRoot, $"d13-tutorial-v4-slot-{slot}.json");
    }
    private void InitializePlayerLifecycle()
    {
        if (!TutorialEnabled) return;
        _ = TutorialSaveRoot; // Validate isolation before any save or settings access.
        if (R2SelfTest && (System.Environment.GetEnvironmentVariable("YUDIAN_PLAYER_GUI_TEST") != "1"
            || DisplayServer.GetName() != "headless"))
            throw new InvalidOperationException("R2 authority tests require GUI_TEST=1, a private absolute slot root, and --headless");
        SetLifecycleBase(_entryOpen ? "home" : "game");
        LoadPlayerSettings();
        RefreshPlayerSlots();
        GetTree().AutoAcceptQuit = false;
    }
    private void CaptureInitialPlayerOnce()
    {
        if (!TutorialEnabled || _initialPlayer != null || !ReadPlayerState().Ready || !_groundRobots.All(a => a.IsOnFloor())) return;
        _initialPlayer = ClonePlayer(CapturePlayer());
        ValidatePlayerSave(_initialPlayer); RefreshPlayerSlots();
    }
    public PlayerLifecycleView ReadLifecycle() => new(TutorialEnabled ? _lifecyclePage : _entryOpen ? "home" : "game",
        _lifecycleBackPage, !_entryOpen, _menuPaused, _userPaused, _playerCommand != null || _loadPending != null,
        _activeSlot, _selectedSlot, _pendingLifecycleAction, PlayerNotice(), _slotViews.ToArray(), _worldEpoch, _worldTransitionRevision);
    private void BeginWorldTransitionAttempt()
    {
        // UI drafts must expire even if validation fails; this is never saved or rolled back.
        _worldTransitionRevision = checked(_worldTransitionRevision + 1);
        ClearPlayerManagement();
    }
    public void QueueLifecycle(string action, int slot = 0, bool confirmed = false)
        => QueuePlayer(new("lifecycle:" + action, Vector3.Zero, -1, null, 0, null, slot, confirmed));
    private void SetLifecycleBase(string page)
    {
        _menuStack.Clear(); _menuStack.Add(page); SyncLifecyclePage();
    }
    private void PushLifecycle(string page)
    {
        if (_menuStack.Count == 0) _menuStack.Add(_entryOpen ? "home" : "game");
        if (_menuStack[^1] != page) _menuStack.Add(page);
        SyncLifecyclePage();
    }
    private void PopLifecycle()
    {
        if (_menuStack.Count > 1) _menuStack.RemoveAt(_menuStack.Count - 1);
        SyncLifecyclePage();
    }
    private void SyncLifecyclePage()
    {
        _lifecyclePage = _menuStack.Count == 0 ? _entryOpen ? "home" : "game" : _menuStack[^1];
        _lifecycleBackPage = _menuStack.Count > 1 ? _menuStack[^2] : _lifecyclePage;
        _menuPaused = !_entryOpen && _lifecyclePage != "game";
        if (_entryOpen || _lifecyclePage != "game") _managementPaused = false;
        PauseGround(_projectionPaused);
    }
    private void RequireLifecycleReady()
    {
        if (_loadPending != null || !ReadPlayerState().Ready) throw new InvalidOperationException("等待世界与实体物理恢复完成");
    }
    private int ResolveLifecycleSlot(int requested) => requested == 0 ? _selectedSlot
        : requested is >= 1 and <= 3 ? requested : throw new ArgumentException("请选择槽位1–3");
    private void ProcessLifecycle(PlayerCommand command)
    {
        if (!TutorialEnabled) { _playerNotice = "本模式使用原工程入口"; return; }
        string action = command.Action[10..];
        if (action is "home" or "quit" or "leave-save" or "leave-discard") ClearPlayerManagement();
        int slot = ResolveLifecycleSlot(command.Slot);
        if (_loadPending != null) throw new InvalidOperationException("世界正在恢复，请稍候");
        switch (action)
        {
            case "pause":
                if (_entryOpen) return;
                if (_lifecyclePage == "game") PushLifecycle("pause");
                _playerNotice = "暂停菜单已打开；原手动暂停保持"; break;
            case "resume":
                if (_entryOpen || _lifecyclePage != "pause") throw new InvalidOperationException("请先返回暂停菜单");
                SetLifecycleBase("game"); _playerNotice = _userPaused ? "保持用户手动暂停" : "模拟继续"; break;
            case "newgame-menu":
                if (!_entryOpen) throw new InvalidOperationException("请先返回主页再开始新游戏");
                RefreshPlayerSlots(); PushLifecycle("newgame"); break;
            case "slots": RefreshPlayerSlots(); PushLifecycle("slots"); break;
            case "settings": PushLifecycle("settings"); break;
            case "back":
                if (_lifecyclePage == "leave") { _pendingLifecycleAction = ""; }
                if (_lifecyclePage == "pause") { SetLifecycleBase("game"); }
                else PopLifecycle();
                break;
            case "select-slot":
                if (command.Slot == 0) throw new ArgumentException("选择槽位需要1–3");
                _selectedSlot = slot; _playerNotice = $"已选择存档槽{slot}"; break;
            case "newgame":
                if (!_entryOpen) throw new InvalidOperationException("请先返回主页再开始新游戏");
                RequireLifecycleReady();
                if (_initialPlayer == null) throw new InvalidOperationException("等待初始世界贴地准备");
                if (File.Exists(TutorialSlotPath(slot)) && !command.Confirmed)
                {
                    _selectedSlot = slot; PushLifecycle("confirm-newgame");
                    _playerNotice = "该槽已有存档；开始后只有显式保存才替换原档"; return;
                }
                BeginPlayerTransition(_initialPlayer, "newgame", slot); break;
            case "load":
                RequireLifecycleReady();
                if (!_entryOpen && !command.Confirmed)
                {
                    _selectedSlot = slot; PushLifecycle("confirm-load");
                    _playerNotice = "读取将替换未保存进度；取消保留当前世界"; return;
                }
                LoadPlayerFrom(TutorialSlotPath(slot), slot); break;
            case "save":
                RequireLifecycleReady();
                if (_entryOpen) throw new InvalidOperationException("请先开始或继续游戏");
                if (SavePlayerTo(TutorialSlotPath(slot))) { _activeSlot = slot; _selectedSlot = slot; RefreshPlayerSlots(); }
                break;
            case "home":
                if (_entryOpen) { SetLifecycleBase("home"); return; }
                _pendingLifecycleAction = "home"; PushLifecycle("leave"); _playerNotice = "离开前可保存当前世界，或丢弃未保存进度"; break;
            case "quit":
                if (_entryOpen) { GetTree().Quit(); return; }
                _pendingLifecycleAction = "quit"; PushLifecycle("leave"); _playerNotice = "退出前可保存当前世界，或丢弃未保存进度"; break;
            case "leave-save":
                if (_lifecyclePage != "leave" || _pendingLifecycleAction.Length == 0) throw new InvalidOperationException("没有待确认的离开操作");
                RequireLifecycleReady();
                if (SavePlayerTo(TutorialSlotPath(_activeSlot))) { RefreshPlayerSlots(); CompletePlayerLeave(); }
                break;
            case "leave-discard":
                if (_lifecyclePage != "leave" || _pendingLifecycleAction.Length == 0) throw new InvalidOperationException("没有待确认的离开操作");
                CompletePlayerLeave(); break;
            case "leave-cancel":
                if (_lifecyclePage != "leave") return;
                _pendingLifecycleAction = ""; SetLifecycleBase("game"); PushLifecycle("pause");
                _playerNotice = "已取消离开；当前世界与用户暂停保留"; break;
            default: throw new ArgumentException("未知生命周期操作，未执行");
        }
    }
    private void CompletePlayerLeave()
    {
        ClearPlayerManagement();
        string action = _pendingLifecycleAction; _pendingLifecycleAction = "";
        _playerCommand = null;
        if (action == "quit") { GetTree().Quit(); return; }
        _entryOpen = true; SetLifecycleBase("home"); _worldEpoch++;
        _playerNotice = "已返回主页；选择新游戏或继续存档";
    }
    private LifecycleRollback CaptureLifecycleRollback() => new(_entryOpen, _menuPaused, _menuStack.ToArray(), _pendingLifecycleAction,
        _activeSlot, _selectedSlot, _worldEpoch, _playerNotice);
    private void RestoreLifecycleRollback(LifecycleRollback saved)
    {
        _entryOpen = saved.EntryOpen; _menuStack.Clear(); _menuStack.AddRange(saved.Stack);
        _pendingLifecycleAction = saved.PendingAction; _activeSlot = saved.ActiveSlot; _selectedSlot = saved.SelectedSlot; _worldEpoch = saved.WorldEpoch;
        SyncLifecyclePage(); _menuPaused = saved.MenuPaused;
    }
    private void CompleteLifecycleTransition(bool recovered)
    {
        ClearPlayerManagement();
        if (!TutorialEnabled) { if (!recovered) _worldEpoch++; return; }
        if (recovered && _lifecycleRollback != null) RestoreLifecycleRollback(_lifecycleRollback);
        else if (!recovered)
        {
            _entryOpen = false; _activeSlot = _transitionSlot; _selectedSlot = _transitionSlot;
            _pendingLifecycleAction = ""; SetLifecycleBase("game"); _worldEpoch++;
            if (_transitionKind == "newgame") _playerNotice = "新教学世界已准备；先观察工程队并规划太阳能";
        }
        _lifecycleRollback = null; RefreshPlayerSlots();
    }
    private void RefreshPlayerSlots()
    {
        if (!TutorialEnabled) return;
        _slotViews = Enumerable.Range(1, 3).Select(ReadPlayerSlot).ToArray();
    }
    private PlayerSlotView ReadPlayerSlot(int slot)
    {
        string path = TutorialSlotPath(slot);
        if (!File.Exists(path)) return new(slot, $"槽位{slot}", false, false, "", "", "空槽", "");
        try
        {
            var saved = ReadPlayerSaveFile(path); ValidatePlayerSave(saved);
            string stage = SavedTutorialStage(saved);
            int built = saved.Bootstrap?.Facilities.Count(f => f.Built) ?? 0;
            return new(slot, $"槽位{slot}", true, true, File.GetLastWriteTimeUtc(path).ToString("O"), stage,
                $"{built}座设施 · 运行{saved.Time:0}秒", "");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            return new(slot, $"槽位{slot}", true, false, "", "", "无法读取", "存档损坏、不支持或无法访问；原档保留");
        }
    }
}

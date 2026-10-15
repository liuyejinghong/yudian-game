#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Godot;

namespace Yudian.PlayerUI;

// 屏幕流：主页 / 暂停 / 三槽 / 新游戏 / 设置 / 覆盖确认 / 读取确认 / 离开确认。
// 页面状态由权威 ReadLifecycle 给出，UI 只渲染并按语义排队意图；菜单期间世界输入不穿透。
public partial class PlayerController
{
    private Control _entryOverlay = null!, _pausePage = null!, _slotsPage = null!, _newGamePage = null!,
        _settingsPage = null!, _confirmNewPage = null!, _confirmLoadPage = null!, _leavePage = null!;
    private Label _entryNotice = null!, _pauseInfoLabel = null!, _slotsHintLabel = null!, _newGameHintLabel = null!,
        _settingsHintLabel = null!, _confirmNewLabel = null!, _confirmLoadLabel = null!, _leaveLabel = null!
        , _settingsStateLabel = null!;
    private Label _homeTitle = null!;
    private Button _entryNewGameButton = null!, _entryLoadButton = null!, _entrySlotsButton = null!,
        _entrySettingsButton = null!, _entryQuitButton = null!;
    private Button _pauseResumeButton = null!, _pauseSaveButton = null!, _pauseSlotsButton = null!,
        _pauseSettingsButton = null!, _pauseHomeButton = null!, _pauseQuitButton = null!;
    private readonly Button[] _slotRows = new Button[3], _newGameRows = new Button[3];
    private Button _slotsPrimaryButton = null!, _slotsSecondaryButton = null!, _slotsBackButton = null!;
    private Button _newGameStartButton = null!, _newGameBackButton = null!;
    private Button _scale100 = null!, _scale125 = null!, _scale150 = null!, _helpDetailButton = null!, _helpBriefButton = null!,
        _settingsBackButton = null!;
    private HSlider _camSpeedSlider = null!;
    private Label _camSpeedLabel = null!;
    private Button _confirmNewOkButton = null!, _confirmNewCancelButton = null!,
        _confirmLoadOkButton = null!, _confirmLoadCancelButton = null!,
        _leaveSaveButton = null!, _leaveDiscardButton = null!, _leaveCancelButton = null!;
    private string _lastShownPage = "";
    private bool _updatingSettingsControls;

    private void BuildScreens(CanvasLayer hud, Control root)
    {
        // —— 主页：居中标题 + 新游戏/继续/存档/设置/退出 ——
        var (home, homeBox) = NewPage(hud, "EntryOverlay", Palette.Scrim, 320);
        _entryOverlay = home;
        _homeTitle = TrackLabel("HomeTitle", "余电", SizeTitle, Palette.Accent);
        _homeTitle.HorizontalAlignment = HorizontalAlignment.Center;
        homeBox.AddChild(_homeTitle);
        var homeSub = TrackLabel("HomeSubtitle", "火星基地经营 · 教学关卡", SizeSmall, Palette.TextDim);
        homeSub.HorizontalAlignment = HorizontalAlignment.Center;
        homeBox.AddChild(homeSub);
        var homeIntro = TrackLabel("HomeIntro", "给机器人目标：自己决定位置、朝向与建设顺序，机器人代劳采矿、运输、加工与建设。", SizeSmall, Palette.Text);
        homeIntro.HorizontalAlignment = HorizontalAlignment.Center;
        homeBox.AddChild(homeIntro);
        _entryNotice = InfoLabel("EntryNotice", SizeSmall, Palette.Warn);
        _entryNotice.HorizontalAlignment = HorizontalAlignment.Center;
        homeBox.AddChild(_entryNotice);
        _entryNewGameButton = MenuButton("EntryNewGameButton", "新游戏", () => QueueLifecycle("newgame-menu"), accent: true);
        _entryLoadButton = MenuButton("EntryLoadButton", "继续游戏", () => QueueLifecycle("load", _homeContinueSlot));
        _entrySlotsButton = MenuButton("EntrySlotsButton", "存 档", () => QueueLifecycle("slots"));
        _entrySettingsButton = MenuButton("EntrySettingsButton", "设 置", () => QueueLifecycle("settings"));
        _entryQuitButton = MenuButton("EntryQuitButton", "退 出", () => QueueLifecycle("quit"));
        homeBox.AddChild(_entryNewGameButton);
        homeBox.AddChild(_entryLoadButton);
        homeBox.AddChild(_entrySlotsButton);
        homeBox.AddChild(_entrySettingsButton);
        homeBox.AddChild(_entryQuitButton);
        LinkVertical(_entryNewGameButton, _entryLoadButton, _entrySlotsButton, _entrySettingsButton, _entryQuitButton);

        // —— 暂停菜单：继续/保存/存档/设置/返回主页/退出 ——
        var (pause, pauseBox) = NewPage(hud, "PausePage", Palette.ScrimLight, 300);
        _pausePage = pause;
        pauseBox.AddChild(CenterTitle("PauseTitle", "暂停菜单"));
        _pauseInfoLabel = InfoLabel("PauseInfoLabel", SizeSmall, Palette.TextDim);
        pauseBox.AddChild(_pauseInfoLabel);
        _pauseResumeButton = MenuButton("PauseResumeButton", "继 续", () => QueueLifecycle("resume"), accent: true);
        _pauseSaveButton = MenuButton("PauseSaveButton", "保存到当前槽", () => QueueLifecycle("save", 0));
        _pauseSlotsButton = MenuButton("PauseSlotsButton", "存 档", () => QueueLifecycle("slots"));
        _pauseSettingsButton = MenuButton("PauseSettingsButton", "设 置", () => QueueLifecycle("settings"));
        _pauseHomeButton = MenuButton("PauseHomeButton", "返回主页", () => QueueLifecycle("home"));
        _pauseQuitButton = MenuButton("PauseQuitButton", "退出游戏", () => QueueLifecycle("quit"));
        pauseBox.AddChild(_pauseResumeButton);
        pauseBox.AddChild(_pauseSaveButton);
        pauseBox.AddChild(_pauseSlotsButton);
        pauseBox.AddChild(_pauseSettingsButton);
        pauseBox.AddChild(_pauseHomeButton);
        pauseBox.AddChild(_pauseQuitButton);
        LinkVertical(_pauseResumeButton, _pauseSaveButton, _pauseSlotsButton, _pauseSettingsButton, _pauseHomeButton, _pauseQuitButton);

        // —— 三槽存档页 ——
        var (slots, slotsBox) = NewPage(hud, "SlotsPage", Palette.ScrimLight, 380);
        _slotsPage = slots;
        slotsBox.AddChild(CenterTitle("SlotsTitle", "存档"));
        _slotsHintLabel = InfoLabel("SlotsHintLabel", SizeSmall, Palette.TextDim);
        slotsBox.AddChild(_slotsHintLabel);
        for (int i = 0; i < 3; i++)
        {
            _slotRows[i] = SlotRowButton("SlotButton_" + (i + 1), i + 1);
            slotsBox.AddChild(_slotRows[i]);
        }
        _slotsPrimaryButton = MenuButton("SlotsPrimaryButton", "读取所选槽", () => QueueLifecycle("load", _slotSelection));
        _slotsSecondaryButton = MenuButton("SlotsSecondaryButton", "保存到所选槽", () => QueueLifecycle("save", _slotSelection));
        _slotsBackButton = MenuButton("SlotsBackButton", "返回", () => QueueLifecycle("back"));
        slotsBox.AddChild(_slotsPrimaryButton);
        slotsBox.AddChild(_slotsSecondaryButton);
        slotsBox.AddChild(_slotsBackButton);
        LinkVertical(_slotRows[0], _slotRows[1], _slotRows[2], _slotsPrimaryButton, _slotsSecondaryButton, _slotsBackButton);

        // —— 新游戏选槽页 ——
        var (newGame, newGameBox) = NewPage(hud, "NewGamePage", Palette.ScrimLight, 380);
        _newGamePage = newGame;
        newGameBox.AddChild(CenterTitle("NewGameTitle", "新游戏 · 选择槽位"));
        _newGameHintLabel = InfoLabel("NewGameHintLabel", SizeSmall, Palette.TextDim);
        newGameBox.AddChild(_newGameHintLabel);
        for (int i = 0; i < 3; i++)
        {
            _newGameRows[i] = SlotRowButton("NewGameSlotButton_" + (i + 1), i + 1);
            newGameBox.AddChild(_newGameRows[i]);
        }
        _newGameStartButton = MenuButton("NewGameStartButton", "开始新游戏", () => QueueLifecycle("newgame", _slotSelection), accent: true);
        _newGameBackButton = MenuButton("NewGameBackButton", "返回", () => QueueLifecycle("back"));
        newGameBox.AddChild(_newGameStartButton);
        newGameBox.AddChild(_newGameBackButton);
        LinkVertical(_newGameRows[0], _newGameRows[1], _newGameRows[2], _newGameStartButton, _newGameBackButton);

        // —— 设置页 ——
        var (settings, settingsBox) = NewPage(hud, "SettingsPage", Palette.ScrimLight, 360);
        _settingsPage = settings;
        settingsBox.AddChild(CenterTitle("SettingsTitle", "设置"));
        _settingsHintLabel = InfoLabel("SettingsHintLabel", SizeSmall, Palette.TextDim);
        settingsBox.AddChild(_settingsHintLabel);
        settingsBox.AddChild(TrackLabel("ScaleCaption", "界面缩放", SizeSmall, Palette.Text));
        var scaleRow = new HBoxContainer();
        scaleRow.AddThemeConstantOverride("separation", 8);
        _scale100 = MenuButton("ScaleButton_100", "100%", () => ChangeScale(1));
        _scale125 = MenuButton("ScaleButton_125", "125%", () => ChangeScale(1.25));
        _scale150 = MenuButton("ScaleButton_150", "150%", () => ChangeScale(1.5));
        foreach (var button in new[] { _scale100, _scale125, _scale150 })
        {
            button.CustomMinimumSize = new Vector2(88, 40);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scaleRow.AddChild(button);
        }
        settingsBox.AddChild(scaleRow);
        settingsBox.AddChild(TrackLabel("CameraCaption", "镜头速度（平移与缩放）", SizeSmall, Palette.Text));
        var camRow = new HBoxContainer();
        camRow.AddThemeConstantOverride("separation", 8);
        _camSpeedSlider = new HSlider { MinValue = 0.5, MaxValue = 2, Step = 0.25, Value = 1, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.All };
        _camSpeedLabel = TrackLabel("CameraSpeedLabel", "1×", SizeBody, Palette.Text, wrap: false);
        _camSpeedLabel.CustomMinimumSize = new Vector2(52, 0);
        _camSpeedSlider.ValueChanged += value =>
        {
            if (_updatingSettingsControls) return;
            _camSpeedLabel.Text = value.ToString("0.##", Inv) + "×";
            ChangeCameraSpeed(value);
        };
        camRow.AddChild(_camSpeedSlider);
        camRow.AddChild(_camSpeedLabel);
        settingsBox.AddChild(camRow);
        settingsBox.AddChild(TrackLabel("HelpCaption", "教学提示详细程度", SizeSmall, Palette.Text));
        var helpRow = new HBoxContainer();
        helpRow.AddThemeConstantOverride("separation", 8);
        _helpDetailButton = MenuButton("HelpDetailButton", "详细", () => ChangeHelp(true));
        _helpBriefButton = MenuButton("HelpBriefButton", "精简", () => ChangeHelp(false));
        foreach (var button in new[] { _helpDetailButton, _helpBriefButton })
        {
            button.CustomMinimumSize = new Vector2(110, 40);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            helpRow.AddChild(button);
        }
        settingsBox.AddChild(helpRow);
        _settingsStateLabel = InfoLabel("SettingsStateLabel", SizeTag, Palette.TextDim);
        settingsBox.AddChild(_settingsStateLabel);
        _settingsBackButton = MenuButton("SettingsBackButton", "返回", () => QueueLifecycle("back"));
        var defaultsButton = MenuButton("SettingsDefaultsButton", "还原默认", RestoreSettingsDefaults);
        settingsBox.AddChild(defaultsButton);
        settingsBox.AddChild(_settingsBackButton);
        // 焦点邻居在全部控件入树后再接线（入树前 GetPath 会报 not in a scene tree）。
        _scale125.FocusNeighborLeft = _scale100.GetPath();
        _scale125.FocusNeighborRight = _scale150.GetPath();
        _scale100.FocusNeighborRight = _scale125.GetPath();
        _scale150.FocusNeighborLeft = _scale125.GetPath();
        _helpDetailButton.FocusNeighborRight = _helpBriefButton.GetPath();
        _helpBriefButton.FocusNeighborLeft = _helpDetailButton.GetPath();
        _scale100.FocusNeighborTop = _settingsBackButton.GetPath();
        _scale125.FocusNeighborTop = _settingsBackButton.GetPath();
        _scale150.FocusNeighborTop = _settingsBackButton.GetPath();
        _camSpeedSlider.FocusNeighborTop = _settingsBackButton.GetPath();
        _helpDetailButton.FocusNeighborTop = _settingsBackButton.GetPath();
        _helpBriefButton.FocusNeighborTop = _settingsBackButton.GetPath();
        _settingsBackButton.FocusNeighborBottom = _camSpeedSlider.GetPath();

        // —— 新游戏覆盖确认 ——
        var (confirmNew, confirmNewBox) = NewPage(hud, "ConfirmNewGamePage", Palette.ScrimLight, 340);
        _confirmNewPage = confirmNew;
        confirmNewBox.AddChild(CenterTitle("ConfirmNewGameTitle", "开始新游戏？"));
        _confirmNewLabel = InfoLabel("ConfirmNewGameLabel", SizeBody, Palette.Text);
        confirmNewBox.AddChild(_confirmNewLabel);
        _confirmNewOkButton = MenuButton("ConfirmNewGameOkButton", "开始新游戏", () => QueueLifecycle("newgame", _slotSelection, confirmed: true), accent: true);
        _confirmNewCancelButton = MenuButton("ConfirmNewGameCancelButton", "取消", () => QueueLifecycle("back"));
        confirmNewBox.AddChild(_confirmNewOkButton);
        confirmNewBox.AddChild(_confirmNewCancelButton);
        LinkVertical(_confirmNewOkButton, _confirmNewCancelButton);

        // —— 读取未保存确认 ——
        var (confirmLoad, confirmLoadBox) = NewPage(hud, "ConfirmLoadPage", Palette.ScrimLight, 340);
        _confirmLoadPage = confirmLoad;
        confirmLoadBox.AddChild(CenterTitle("ConfirmLoadTitle", "读取存档？"));
        _confirmLoadLabel = InfoLabel("ConfirmLoadLabel", SizeBody, Palette.Text);
        confirmLoadBox.AddChild(_confirmLoadLabel);
        _confirmLoadOkButton = MenuButton("ConfirmLoadOkButton", "读取（放弃当前进度）", () => QueueLifecycle("load", _slotSelection, confirmed: true), accent: true);
        _confirmLoadCancelButton = MenuButton("ConfirmLoadCancelButton", "取消", () => QueueLifecycle("back"));
        confirmLoadBox.AddChild(_confirmLoadOkButton);
        confirmLoadBox.AddChild(_confirmLoadCancelButton);
        LinkVertical(_confirmLoadOkButton, _confirmLoadCancelButton);

        // —— 离开确认（返回主页 / 退出 / 系统关窗共用同一边界） ——
        var (leave, leaveBox) = NewPage(hud, "LeavePage", Palette.ScrimLight, 340);
        _leavePage = leave;
        leaveBox.AddChild(CenterTitle("LeaveTitle", "离开当前游戏？"));
        _leaveLabel = InfoLabel("LeaveLabel", SizeBody, Palette.Text);
        leaveBox.AddChild(_leaveLabel);
        _leaveSaveButton = MenuButton("LeaveSaveButton", "保存并离开", () => QueueLifecycle("leave-save"), accent: true);
        _leaveDiscardButton = MenuButton("LeaveDiscardButton", "不保存离开", () => QueueLifecycle("leave-discard"));
        _leaveCancelButton = MenuButton("LeaveCancelButton", "取消", () => QueueLifecycle("leave-cancel"));
        leaveBox.AddChild(_leaveSaveButton);
        leaveBox.AddChild(_leaveDiscardButton);
        leaveBox.AddChild(_leaveCancelButton);
        LinkVertical(_leaveSaveButton, _leaveDiscardButton, _leaveCancelButton);
    }

    private (ColorRect Page, VBoxContainer Box) NewPage(CanvasLayer hud, string name, Color scrim, int panelWidth)
    {
        var page = new ColorRect { Name = name, Color = scrim, Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        page.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hud.AddChild(page);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        page.AddChild(center);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        center.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        box.CustomMinimumSize = new Vector2(panelWidth, 0);
        _minSizeTargets.Add((box, new Vector2(panelWidth, 0)));
        panel.AddChild(box);
        return (page, box);
    }

    private Label CenterTitle(string name, string text)
    {
        var label = TrackLabel(name, text, SizeHeading, Palette.Text);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private Button SlotRowButton(string name, int slot)
    {
        var button = new Button
        {
            Name = name,
            Text = "槽 " + slot,
            Alignment = HorizontalAlignment.Left,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(300, 56),
            AutowrapMode = TextServer.AutowrapMode.Arbitrary,
        };
        _sizeTargets.Add((button, SizeSmall));
        _minSizeTargets.Add((button, new Vector2(300, 56)));
        StyleButton(button, accent: false, focusable: true);
        button.Pressed += () =>
        {
            _slotSelection = slot;
            _slotTouchedAt = Time.GetTicksMsec() / 1000.0;
            QueueLifecycle("select-slot", slot);
        };
        return button;
    }

    private static void LinkVertical(params Button[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].FocusNeighborTop = buttons[(i - 1 + buttons.Length) % buttons.Length].GetPath();
            buttons[i].FocusNeighborBottom = buttons[(i + 1) % buttons.Length].GetPath();
        }
    }

    private void GrabFirstEnabled(params Button[] buttons)
    {
        foreach (var button in buttons)
        {
            if (!button.Disabled && button.IsVisibleInTree())
            {
                button.GrabFocus();
                return;
            }
        }
    }

    // ---- 页面刷新 ----

    private void RefreshScreens(PlayerLifecycleView lifecycle, PlayerReadModel state)
    {
        _entryOverlay.Visible = lifecycle.Page == "home";
        _pausePage.Visible = lifecycle.Page == "pause";
        _slotsPage.Visible = lifecycle.Page == "slots";
        _newGamePage.Visible = lifecycle.Page == "newgame";
        _settingsPage.Visible = lifecycle.Page == "settings";
        _confirmNewPage.Visible = lifecycle.Page == "confirm-newgame";
        _confirmLoadPage.Visible = lifecycle.Page == "confirm-load";
        _leavePage.Visible = lifecycle.Page == "leave";

        if (_entryOverlay.Visible) RefreshHome(lifecycle, state);
        if (_pausePage.Visible) RefreshPause(lifecycle);
        if (_slotsPage.Visible) RefreshSlotList(lifecycle, state, _slotRows, forNewGame: false);
        if (_newGamePage.Visible) RefreshSlotList(lifecycle, state, _newGameRows, forNewGame: true);
        if (_settingsPage.Visible) RefreshSettings();
        if (_confirmNewPage.Visible) RefreshConfirmNew(lifecycle);
        if (_confirmLoadPage.Visible) RefreshConfirmLoad(lifecycle);
        if (_leavePage.Visible) RefreshLeave(lifecycle);

        if (lifecycle.Page != _lastShownPage)
        {
            _lastShownPage = lifecycle.Page;
            FocusDefaultFor(lifecycle.Page);
        }
    }

    private void FocusDefaultFor(string page)
    {
        switch (page)
        {
            case "home": GrabFirstEnabled(_entryNewGameButton, _entryLoadButton, _entrySlotsButton, _entrySettingsButton, _entryQuitButton); break;
            case "pause": GrabFirstEnabled(_pauseResumeButton, _pauseSaveButton, _pauseSlotsButton, _pauseSettingsButton, _pauseHomeButton, _pauseQuitButton); break;
            case "slots": GrabFirstEnabled(_slotRows[_slotSelection - 1], _slotRows[0], _slotsPrimaryButton, _slotsBackButton); break;
            case "newgame": GrabFirstEnabled(_newGameRows[_slotSelection - 1], _newGameRows[0], _newGameStartButton, _newGameBackButton); break;
            case "settings": GrabFirstEnabled(ScaleButton(_settingsView?.UiScale ?? 1), _settingsBackButton); break;
            case "confirm-newgame": _confirmNewCancelButton.GrabFocus(); break; // 覆盖类确认默认停在安全项
            case "confirm-load": _confirmLoadCancelButton.GrabFocus(); break;
            case "leave": _leaveSaveButton.GrabFocus(); break;
            case "game": GetViewport().GuiReleaseFocus(); break;
        }
    }

    private Button ScaleButton(double scale) => scale switch
    {
        >= 1.5 => _scale150,
        >= 1.25 => _scale125,
        _ => _scale100,
    };

    // 还原默认只改本 app 的三项真实设置（100% / 1× / 详细），不触碰系统设置。
    private void RestoreSettingsDefaults()
    {
        _draftScale = 1;
        _draftSpeed = 1;
        _draftHelp = "detailed";
        QueueSettingsFromDraft();
    }

    private void RefreshHome(PlayerLifecycleView lifecycle, PlayerReadModel state)
    {
        _entryNotice.Text = lifecycle.Notice.Length > 0 ? lifecycle.Notice : state.Notice;
        _entryNotice.Visible = _entryNotice.Text.Length > 0;
        bool busy = lifecycle.Busy;
        bool ready = state.Ready;
        var (canContinue, slot, label) = HomeContinueTarget(lifecycle, state);
        _homeContinueSlot = slot;
        _entryNewGameButton.Disabled = busy || !ready;
        _entryLoadButton.Text = label;
        _entryLoadButton.Disabled = busy || !ready || !canContinue;
        _entrySlotsButton.Disabled = busy;
        _entrySettingsButton.Disabled = busy;
        _entryQuitButton.Disabled = busy;
    }

    private int _homeContinueSlot;

    // 主页"继续游戏"目标槽：优先当前所选（仍可载入），否则最近 SavedAtUtc 的可载入槽，全无则禁用。
    // 明确传实际槽号，不发明新 action、不改 slot 0 协议；槽列表为空时退回旧"当前所选槽"语义。
    private (bool Enabled, int Slot, string Label) HomeContinueTarget(PlayerLifecycleView lifecycle, PlayerReadModel state)
    {
        if (lifecycle.Slots.Length > 0)
        {
            int slot = ContinueTargetSlot(lifecycle.Slots, _slotSelection);
            if (slot == 0) return (false, 0, "继续游戏");
            var view = lifecycle.Slots.FirstOrDefault(s => s.Slot == slot);
            string time = view is { } v ? LocalTimeText(v.SavedAtUtc) : "";
            return (true, slot, "继续游戏 · 槽 " + slot + (time.Length > 0 ? " · " + time : ""));
        }
        return (state.SaveExists, 0, "继续游戏");
    }

    internal static int ContinueTargetSlot(PlayerSlotView[] slots, int selectedSlot)
    {
        var selected = slots.FirstOrDefault(s => s.Slot == selectedSlot);
        if (selected is { Loadable: true }) return selected.Slot;
        var recent = slots.Where(s => s.Loadable).OrderByDescending(s => SafeSavedTime(s.SavedAtUtc)).ThenBy(s => s.Slot).FirstOrDefault();
        return recent?.Slot ?? 0;
    }

    private static DateTime SafeSavedTime(string savedAtUtc)
        => DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : DateTime.MinValue;

    private static string LocalTimeText(string savedAtUtc)
    {
        var time = SafeSavedTime(savedAtUtc);
        return time == DateTime.MinValue ? "" : time.ToLocalTime().ToString("MM-dd HH:mm", Inv);
    }

    // 只消费已冻结的阶段 id；未识别的阶段不把内部名展示给玩家。
    private static string StageDisplayForSlot(string stage) => stage switch
    {
        "observe-team" => "认识工程队",
        "solar" => "太阳能选址",
        "charger" or "charger-wire" => "充电桩",
        "repair" or "repair-wire" => "维修站",
        "processor" or "processor-wire" => "加工设施",
        "first-direction" => "首个发展方向",
        "second-direction" => "第二发展方向",
        "second-charger" or "second-charger-wire" => "第二充电桩",
        "conclusion" => "教学完成",
        "freeplay" => "自由经营",
        "legacy" => "练习模式",
        "" => "",
        _ => "教学进行中",
    };

    private void RefreshPause(PlayerLifecycleView lifecycle)
    {
        _pauseInfoLabel.Text = (lifecycle.Notice.Length > 0 ? lifecycle.Notice + "\n" : "") +
            "当前槽 " + lifecycle.ActiveSlot + (lifecycle.UserPaused ? " · 已手动暂停（继续后保持）" : "");
        _pauseSaveButton.Disabled = lifecycle.Busy || !(_lifecycle?.InGame ?? false);
        _pauseHomeButton.Disabled = lifecycle.Busy;
        _pauseQuitButton.Disabled = lifecycle.Busy;
    }

    private void RefreshSlotList(PlayerLifecycleView lifecycle, PlayerReadModel state, Button[] rows, bool forNewGame)
    {
        for (int i = 0; i < 3; i++)
        {
            int slot = i + 1;
            PlayerSlotView? view = lifecycle.Slots.FirstOrDefault(s => s.Slot == slot);
            string text;
            if (view is { } s)
            {
                text = "槽 " + slot + (s.Label.Length > 0 ? " · " + s.Label : "");
                if (!s.Exists) text += "\n空槽位";
                else
                {
                    // 槽摘要给玩家：本地时间与目标名称；不显示 raw ISO 或内部阶段 id。
                    var line2 = string.Join(" · ", new[] { LocalTimeText(s.SavedAtUtc), StageDisplayForSlot(s.TutorialStage) }.Where(v => v.Length > 0));
                    if (line2.Length > 0) text += "\n" + line2;
                    if (s.Summary.Length > 0) text += "\n" + s.Summary;
                    if (!s.Loadable && s.Error.Length > 0) text += "\n⚠ 无法读取：" + s.Error;
                    else if (!s.Loadable) text += "\n⚠ 存档不可读取";
                }
            }
            else
            {
                text = "槽 " + slot + "\n（存档列表由权威提供）";
            }
            rows[i].Text = text;
            bool selected = slot == _slotSelection;
            rows[i].AddThemeStyleboxOverride("normal", selected
                ? ButtonStyle(Palette.PanelBg.Lightened(0.06f), Palette.Accent, 2)
                : ButtonStyle(Palette.PanelBg));
            rows[i].AddThemeStyleboxOverride("hover", ButtonStyle(Palette.PanelBg.Lightened(0.1f)));
            rows[i].AddThemeColorOverride("font_color", Palette.Text);
            rows[i].AddThemeColorOverride("font_hover_color", Palette.Text);
        }

        var selectedView = lifecycle.Slots.FirstOrDefault(s => s.Slot == _slotSelection);
        bool canUseSelection = selectedView is { Exists: true, Loadable: true };
        if (forNewGame)
        {
            _newGameHintLabel.Text = selectedView is { Exists: true }
                ? "该槽已有存档：开始前会先确认；直到显式保存前，旧档不会被覆盖。"
                : "选择空槽位；新世界用初始快照重置，教学从零开始。";
            _newGameStartButton.Disabled = lifecycle.Busy || !state.Ready;
        }
        else
        {
            _slotsHintLabel.Text = lifecycle.InGame
                ? "保存到所选槽（成功后才成为当前槽）；读取会先确认并放弃当前进度。"
                : "选择槽位后读取；坏档会显示原因并留在本页。";
            _slotsSecondaryButton.Visible = lifecycle.InGame;
            _slotsPrimaryButton.Disabled = lifecycle.Busy || !state.Ready || !canUseSelection;
            _slotsSecondaryButton.Disabled = lifecycle.Busy || !state.Ready;
            // 次要按钮隐藏时，焦点顺序直接落到返回。
            _slotsPrimaryButton.FocusNeighborBottom = (_slotsSecondaryButton.Visible ? _slotsSecondaryButton : _slotsBackButton).GetPath();
            _slotsBackButton.FocusNeighborTop = (_slotsSecondaryButton.Visible ? _slotsSecondaryButton : _slotsPrimaryButton).GetPath();
        }
    }

    private void RefreshSettings()
    {
        var values = _settingsDirty
            ? new PlayerSettingsView(_draftScale, _draftSpeed, _draftHelp, _settingsView?.Revision ?? 0)
            : _settingsView;
        double scale = values?.UiScale ?? 1;
        double speed = values?.CameraSpeed ?? 1;
        string help = values?.HelpDetail ?? "detailed";
        HighlightToggle(_scale100, scale < 1.125);
        HighlightToggle(_scale125, scale >= 1.125 && scale < 1.375);
        HighlightToggle(_scale150, scale >= 1.375);
        HighlightToggle(_helpDetailButton, help != "brief");
        HighlightToggle(_helpBriefButton, help == "brief");
        _updatingSettingsControls = true;
        _camSpeedSlider.Value = speed;
        _updatingSettingsControls = false;
        _camSpeedLabel.Text = speed.ToString("0.##", Inv) + "×";
        _settingsHintLabel.Text = "设置实际生效：界面缩放影响布局与字号；镜头速度影响平移与缩放；精简提示仍显示当前目标。";
        _settingsStateLabel.Text = _settingsDirty ? "已请求新值，等待权威回显…" : "当前生效值来自权威设置。";
    }

    private static void HighlightToggle(Button button, bool on)
    {
        button.AddThemeStyleboxOverride("normal", on
            ? ButtonStyle(Palette.Accent, Palette.Accent, 0)
            : ButtonStyle(Palette.PanelBg));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(on ? Palette.Accent.Lightened(0.1f) : Palette.PanelBg.Lightened(0.1f)));
        var fg = on ? Palette.AccentText : Palette.Text;
        button.AddThemeColorOverride("font_color", fg);
        button.AddThemeColorOverride("font_hover_color", fg);
        button.AddThemeColorOverride("font_pressed_color", fg);
    }

    private void RefreshConfirmNew(PlayerLifecycleView lifecycle)
    {
        var view = lifecycle.Slots.FirstOrDefault(s => s.Slot == _slotSelection);
        string slotSummary = view is { Exists: true }
            ? "槽 " + _slotSelection + (view.SavedAtUtc.Length > 0 ? "（" + view.SavedAtUtc + "）" : "") + " 已有存档。"
            : "槽 " + _slotSelection + " 当前为空。";
        _confirmNewLabel.Text = slotSummary + "\n开始新游戏会用初始快照真正重置世界；该槽直到你显式保存前不会被覆盖。";
        _confirmNewOkButton.Disabled = lifecycle.Busy;
    }

    private void RefreshConfirmLoad(PlayerLifecycleView lifecycle)
    {
        _confirmLoadLabel.Text = "读取槽 " + _slotSelection + " 会替换当前世界与进度；未保存的当前进度将丢失。\n校验或物理失败会保留当前世界、菜单与暂停状态。";
        _confirmLoadOkButton.Disabled = lifecycle.Busy;
    }

    private void RefreshLeave(PlayerLifecycleView lifecycle)
    {
        string destination = lifecycle.PendingAction switch
        {
            "quit" => "退出游戏",
            "home" => "返回主页",
            _ => "离开当前游戏",
        };
        _leaveLabel.Text = destination + "前是否保存当前进度？\n保存失败会留在本页，当前世界与旧有效档保留。";
        _leaveSaveButton.Disabled = lifecycle.Busy;
        _leaveDiscardButton.Disabled = lifecycle.Busy;
    }
}

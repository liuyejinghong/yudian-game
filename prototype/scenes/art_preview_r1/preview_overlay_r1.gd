extends Control
## ART-PREVIEW-01 slice B: annotation overlay rendered INSIDE the capture
## SubViewport. Evidence mode shows asset/revision, camera/yaw, state/phase/
## phase_t, cargo, reason, time, the contract reason icons (battery / wrench /
## both / recharge arrow) and socket auxiliary markers. Blind mode shows
## nothing (no answer, no icons, no socket aids).
##
## Functional evidence annotation required by requirements/preview-r1.md and
## visual-acceptance-r1.md; not an art deliverable. Uses the local system font
## (PingFang SC); nothing is downloaded.

const REASON_NONE := 0
const REASON_BATTERY := 1
const REASON_WRENCH := 2
const REASON_BOTH := 3
const REASON_ARROW := 4

const INFO_POS := Vector2(28, 24)
const ICON_AREA_POS := Vector2(1920 - 28 - 320, 1200 - 36 - 120)
const ICON_AREA_SIZE := Vector2(320, 120)
const FONT_SIZE := 30

var labels_mode := "evidence"
var reason := "none"
var socket_points: Array = []  # [{position: Vector2, visible: bool}]
var info_text := ""

var _font: SystemFont
var _icon_kind := REASON_NONE

@onready var _info_label: Label = Label.new()


func _init() -> void:
	name = "AnnotationOverlay"
	size = Vector2(1920, 1200)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_font = SystemFont.new()
	_font.font_names = PackedStringArray(["PingFang SC", "PingFangSC", "Heiti SC", "STHeiti"])


func _ready() -> void:
	_info_label.position = INFO_POS
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.0, 0.0, 0.0, 0.55)
	style.set_corner_radius_all(8)
	style.set_content_margin_all(12)
	_info_label.add_theme_stylebox_override("normal", style)
	_info_label.add_theme_font_override("font", _font)
	_info_label.add_theme_font_size_override("font_size", FONT_SIZE)
	_info_label.add_theme_color_override("font_color", Color.WHITE)
	add_child(_info_label)
	_apply_visibility()


func set_info_text(text: String) -> void:
	info_text = text
	if _info_label != null:
		_info_label.text = text


func set_labels_mode(mode: String) -> void:
	labels_mode = mode
	_icon_kind = _reason_icon(reason)
	_apply_visibility()
	queue_redraw()


func set_reason(value: String) -> void:
	reason = value
	_icon_kind = _reason_icon(value)
	queue_redraw()


func set_socket_points(points: Array) -> void:
	socket_points = points
	queue_redraw()


func _apply_visibility() -> void:
	if _info_label == null:
		return
	_info_label.visible = labels_mode == "evidence"


static func _reason_icon(value: String) -> int:
	match value:
		"no_power":
			return REASON_BATTERY
		"mechanical":
			return REASON_WRENCH
		"both":
			return REASON_BOTH
		"return_charge":
			return REASON_ARROW
	return REASON_NONE


func _draw() -> void:
	if labels_mode != "evidence":
		return
	if _icon_kind != REASON_NONE:
		_draw_reason_icon(_icon_kind, ICON_AREA_POS, ICON_AREA_SIZE)
	for sp: Variant in socket_points:
		var entry: Dictionary = sp
		if not bool(entry.get("visible", false)):
			continue
		_draw_socket_marker(entry["position"])


func _draw_reason_icon(kind: int, top_left: Vector2, area: Vector2) -> void:
	var bg := Rect2(top_left, area)
	draw_rect(bg, Color(0.0, 0.0, 0.0, 0.55))
	var single := Vector2(area.x / 2.0, area.y)
	var slots := [Rect2(top_left, single), Rect2(top_left + Vector2(single.x, 0.0), single)]
	match kind:
		REASON_BATTERY:
			_draw_battery(slots[0])
		REASON_WRENCH:
			_draw_wrench(slots[0])
		REASON_BOTH:
			_draw_battery(slots[0])
			_draw_wrench(slots[1])
		REASON_ARROW:
			_draw_recharge_arrow(slots[0])


# Battery outline (empty cell), contract icon for no_power.
func _draw_battery(cell: Rect2) -> void:
	var body := Rect2(cell.position + Vector2(30, 30), Vector2(80, 40))
	var nub := Rect2(body.position + Vector2(body.size.x, 14), Vector2(10, 12))
	draw_rect(body, Color.WHITE, false, 4.0)
	draw_rect(nub, Color.WHITE, false, 4.0)
	# empty bars hint
	draw_rect(Rect2(body.position + Vector2(8, 8), Vector2(20, 24)), Color.WHITE, false, 3.0)


# Simplified wrench, contract icon for mechanical.
func _draw_wrench(cell: Rect2) -> void:
	var center := cell.position + cell.size * 0.5
	var dir := Vector2(1, 1).normalized()
	var handle_a := center - dir * 34.0
	var handle_b := center + dir * 34.0
	draw_line(handle_a, handle_b, Color.WHITE, 10.0)
	draw_arc(center, 20.0, 0.55, TAU - 0.55, 24, Color.WHITE, 8.0, true)
	draw_circle(center, 6.0, Color.WHITE)


# Arrow toward a charging base, contract icon for return_charge.
func _draw_recharge_arrow(cell: Rect2) -> void:
	var base := Rect2(cell.position + Vector2(30, 40), Vector2(30, 40))
	draw_rect(base, Color.WHITE, false, 4.0)
	var y := cell.size.y * 0.5  # local offset; cell.position is added once below
	var tip := cell.position + Vector2(cell.size.x * 0.72, y)
	var tail := cell.position + Vector2(cell.size.x * 0.22, y)
	draw_line(tail, tip, Color.WHITE, 8.0)
	draw_colored_polygon(PackedVector2Array([
		tip, tip + Vector2(-18, -12), tip + Vector2(-18, 12),
	]), Color.WHITE)


func _draw_socket_marker(pos: Vector2) -> void:
	draw_arc(pos, 14.0, 0, TAU, 32, Color(1.0, 0.85, 0.2), 3.0, true)
	draw_line(pos + Vector2(-22, 0), pos + Vector2(22, 0), Color(1.0, 0.85, 0.2), 2.0)
	draw_line(pos + Vector2(0, -22), pos + Vector2(0, 22), Color(1.0, 0.85, 0.2), 2.0)
	draw_string(_font, pos + Vector2(20, -20), "Socket", HORIZONTAL_ALIGNMENT_LEFT, -1, 24)

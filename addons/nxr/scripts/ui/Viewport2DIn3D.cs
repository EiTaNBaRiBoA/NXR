using System;
using Godot;
using NXR;


/// <summary>
/// Handles rendering 2D UI in a 3D scene.
/// </summary>
[Tool]
[GlobalClass]
public partial class Viewport2DIn3D : Node3D, IPointerInteractable
{
	/// <summary>
	/// The UI scene. Usually a Control scene with common interactable elements
	/// that the player can click with their trigger button.
	/// </summary>
	[Export]
	public PackedScene SubScene
	{
		get => _subScene;
		set
		{
			_subScene = value;
			_dirty |= Dirty.SubScene;

			if (!_isReady) return;

			UpdateRender();
		}
	}

	/// <summary>
	/// The mesh on which the SubViewport content will be drawn on.
	/// </summary>
	[Export]
	public MeshInstance3D Screen { set; get; }

	/// <summary>
	/// How often the SubViewport will update.
	/// </summary>


	[Export]
	public UpdateMode ViewportUpdateMode
	{
		set
		{
			_dirty |= Dirty.Update;

			_updateMode = value;

			UpdateRender();
		}

		get => _updateMode;
	}

	/// <summary>
	/// Allows manual control of the flags in editor to trigger updates.
	/// </summary>
	[ExportGroup("Flag Controls")]
	[Export]
	public bool ReapplyRedraw
	{
		set => _dirty |= Dirty.Redraw;
		get => false;
	}
	[Export]
	public bool ReapplyMaterial
	{
		set => _dirty |= Dirty.Material;
		get => false;
	}
	[Export]
	public bool ReapplySurface
	{
		set => _dirty |= Dirty.Surface;
		get => false;
	}
	[Export]
	public bool ReapplyAlbedo
	{
		set => _dirty |= Dirty.Albedo;
		get => false;
	}
	[Export]
	public bool ReapplySubscene
	{
		set => _dirty |= Dirty.SubScene;
		get => false;
	}


	public Pointer CurrentPointer { get; set; }
	public SubViewport SubViewport { set; get; }
	public StaticBody3D CollisionObject { set; get; }


	public enum UpdateMode
	{
		Once,
		Always,
		Throttled
	}

	[Flags]
	private enum Dirty
	{
		None = 0,
		Material = 1,
		SubScene = 2,
		Size = 4,
		Albedo = 8,
		Update = 16,
		Surface = 32,
		Redraw = 64,
		All = 127
	}

	private PackedScene _subScene;
	private double _timeSinceUpdate = 0;
	private Control _subSceneInstance;
	private Dirty _dirty = Dirty.All;
	private bool _isReady = false;
	private StandardMaterial3D _screenMaterial;
	private Vector2 _screenSize;
	private UpdateMode _updateMode = UpdateMode.Throttled;
	private Vector2 _pressedPos = new();
	private Vector2 _prevPressedPos = new();

	public override void _Ready()
	{
		_isReady = true;

		SubViewport = GetNode<SubViewport>("%SubViewport");
		CollisionObject = GetNode<StaticBody3D>("%CollisionObject");

		if (SubViewport.GetChild(0) != null && Util.NodeIs(SubViewport.GetChild(0), typeof(Control)))
		{
			_subSceneInstance = SubViewport.GetChild(0) as Control;
		}

		Update();
	}


	public override void _Process(double delta)
	{
		if (Screen is null) return;

		CheckScreenProperties();

		if (Engine.IsEditorHint())
		{
			_timeSinceUpdate += delta;
			if (_timeSinceUpdate >= 1.0)
			{
				_timeSinceUpdate = 0;
				_dirty |= Dirty.Size;

				Update();
			}
		}
		else if (_updateMode == UpdateMode.Throttled)
		{
			float frameTime = 1 / 30;   // 30 update fps
			_timeSinceUpdate += delta;
			if (_timeSinceUpdate > frameTime)
			{

				_timeSinceUpdate = 0;

				SubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
			}
		}
	}

	/// <summary>
	/// Check all Dirty flags for any needed updates
	/// </summary>
	private void Update()
	{
		UpdateRender();

		if (_dirty.HasFlag(Dirty.Size))
		{
			_dirty &= ~Dirty.Albedo;
			_dirty |= Dirty.Albedo | Dirty.Material;

			if (Screen is not null)
				_screenSize = (Screen.Mesh as PlaneMesh).Size;

			if (CollisionObject is null) return;
			if (CollisionObject.GetChild(0) is null) return;
			(CollisionObject.GetChild<CollisionShape3D>(0).Shape as BoxShape3D).Size = new Vector3(_screenSize.X, _screenSize.Y, 0.01f);
		}
	}

	/// <summary>
	/// Checking Dirty flags for render updates
	/// </summary>
	private void UpdateRender()
	{
		if (Engine.IsEditorHint())  // Get references if running in editor
		{
			SubViewport = GetNode<SubViewport>("%SubViewport");
		}

		if (_dirty.HasFlag(Dirty.Material))
		{
			_dirty &= ~Dirty.Material;


			_screenMaterial = new StandardMaterial3D
			{
				CullMode = BaseMaterial3D.CullModeEnum.Disabled,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			};

			_dirty |= Dirty.Albedo | Dirty.Surface;
		}

		if (_dirty.HasFlag(Dirty.SubScene))
		{
			_dirty &= ~Dirty.SubScene;

			if (SubViewport.GetChild(0) != null && Util.NodeIs(SubViewport.GetChild(0), typeof(Control)))
			{
				_subSceneInstance = SubViewport.GetChild(0) as Control;
				return;
			}

			if (_subSceneInstance is not null && IsInstanceValid(_subSceneInstance))
			{
				SubViewport.RemoveChild(_subSceneInstance);
				_subSceneInstance.QueueFree();
			}

			if (_subScene is not null)
			{
				_subSceneInstance = _subScene.Instantiate<Control>();
				SubViewport.AddChild(_subSceneInstance);
			}

			_dirty |= Dirty.Redraw;
		}

		if (_dirty.HasFlag(Dirty.Albedo))
		{
			_dirty &= ~Dirty.Albedo;

			_screenMaterial.AlbedoTexture = SubViewport.GetTexture();
		}

		if (_dirty.HasFlag(Dirty.Surface))
		{
			_dirty &= ~Dirty.Surface;

			Screen?.SetSurfaceOverrideMaterial(0, _screenMaterial);
		}

		if (_dirty.HasFlag(Dirty.Redraw))
		{
			_dirty &= ~Dirty.Redraw;

			if (Engine.IsEditorHint()) SubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		}

		if (_dirty.HasFlag(Dirty.Update))
		{
			_dirty &= ~Dirty.Update;

			if (Engine.IsEditorHint() || _updateMode == UpdateMode.Once)
			{
				SubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
			}
			else if (_updateMode == UpdateMode.Always)
			{
				SubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
			}
			else if (_updateMode == UpdateMode.Throttled)
			{
				SubViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
			}
		}
	}


	/// <summary>
	/// Monitoring Screen's size to see if the collision needs to be updated.
	/// </summary>
	private void CheckScreenProperties()
	{
		if (Screen != null && (Screen.Mesh as PlaneMesh).Size != _screenSize)
		{
			_dirty |= Dirty.Size;

			Update();
		}
	}

	public CollisionShape3D GetCollisionShape()
	{
		return CollisionObject.GetChild<CollisionShape3D>(0);
	}

	public Control GetSubsceneInstance()
	{
		return _subSceneInstance;
	}

	private Vector2 GetVPLocalPoint(Vector3 point)
	{
		Vector3 localPoint = new();
		CollisionShape3D shape = GetCollisionShape();
		Vector3 shapeSize = (Vector3)shape.Shape.Get("size");
		localPoint = CollisionObject.ToLocal(point);
		localPoint /= new Vector3(shapeSize.X, shapeSize.Y, shapeSize.Z);
		localPoint += new Vector3(0.5f, -0.5f, 0f);

		return new Vector2(localPoint.X, -localPoint.Y) * SubViewport.Size;
	}


	public void PointerEntered()
	{
	}

	public void PointerExited()
	{
	}

	public void Pressed(Vector3 where)
	{
		InputEventMouseButton clickEvent = new();
		clickEvent.Pressed = true;
		clickEvent.ButtonIndex = MouseButton.Left;
		clickEvent.Position = GetVPLocalPoint(where);
		SubViewport.GetViewport().PushInput(clickEvent);

		_pressedPos = GetVPLocalPoint(where);
	}


	public void Released(Vector3 where)
	{
		InputEventMouseButton clickEvent = new();
		clickEvent.Pressed = false;
		clickEvent.ButtonIndex = MouseButton.Left;
		clickEvent.Position = GetVPLocalPoint(where);
		SubViewport.GetViewport().PushInput(clickEvent);
	}


	public void Moved(Vector3 where)
	{
		InputEventMouseMotion eventMotion = new InputEventMouseMotion();
		eventMotion.Position = GetVPLocalPoint(where);
		eventMotion.GlobalPosition = GetVPLocalPoint(where);
		eventMotion.Relative = GetVPLocalPoint(where) - _prevPressedPos;
		eventMotion.Pressure = 1.0f;
		eventMotion.ButtonMask = Godot.MouseButtonMask.Left;
		SubViewport.PushInput(eventMotion);

		_prevPressedPos = GetVPLocalPoint(where);
	}
}
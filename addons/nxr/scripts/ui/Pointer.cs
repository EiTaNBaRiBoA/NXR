using Godot;
using NXR;
using NXRInteractable;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;


enum PointerInputType
{
	Press,
	Release,
	Move,
}


public partial class Pointer : RayCast3D
{

	#region Exported
	[Export] private Controller _controller;
	[Export] public bool Disabled  = false; 

	[Export(PropertyHint.Range,"0.0, 100.0")] private float _visiblePercent = 100.0f; 
	[Export] private float _defaultDistance = 2.0f; 
	[Export(PropertyHint.Range, "0.0, 1.0")] private float _velocityStrength = 0.1f;


	[ExportGroup("Actions")]
	[Export] private string _pressAction = "trigger_click";
	[Export] private string _releaseAction = "trigger_click";


	[ExportGroup("InteractorSettings")]
	[Export] Interactor _interactor; 
	[Export] bool _disableWhenGrabbing = true; 
	#endregion

	private IPointerInteractable _prevInteractable; 



	public override void _Ready()
	{
		if (_controller != null)
		{
			_controller.ButtonPressed += OnButtonPressed;
			_controller.ButtonReleased += OnButtonReleased;
		}

		if (_interactor != null) 
		{
			_interactor.Grabbed += OnGrabbed; 
			_interactor.Dropped += OnDropped; 
		}
	}


	public override void _Process(double delta)
	{
		if (Disabled) return; 

		TrySendInput(PointerInputType.Move);
		ManageCurve(delta); 


		// handle pointer enter 
		if (_prevInteractable == null && GetPointerInteractable() != null) 
		{ 
			_prevInteractable = GetPointerInteractable(); 
			_prevInteractable.PointerEntered(); 
		}

		// handle pointer exit 
		if (GetCollider() == null && _prevInteractable != null) { 
			 _prevInteractable.PointerExited(); 
			 _prevInteractable = null; 
		}
	}


	private void OnButtonPressed(String button)
	{
		if (button != _pressAction) return; 
		
		TrySendInput(PointerInputType.Press);
	}


	private void OnButtonReleased(String button)
	{
		if (button != _pressAction) return; 
		
		TrySendInput(PointerInputType.Release);
	}


	private void TrySendInput(PointerInputType type)
	{
		if (GetPointerInteractable() == null) return;

		IPointerInteractable interactable = (IPointerInteractable)Util.GetParentOrOwnerOfType((Node)GetCollider(), typeof(IPointerInteractable));

		switch (type)
		{
			case PointerInputType.Press:
				interactable.Pressed(GetCollisionPoint());
				break;
			case PointerInputType.Release:
				interactable.Released(GetCollisionPoint());
				break;
			case PointerInputType.Move:
				interactable.Moved(GetCollisionPoint());
				break;
		}
	}


	private IPointerInteractable GetPointerInteractable()
	{
		return (IPointerInteractable)Util.GetParentOrOwnerOfType(
			(Node)GetCollider(),
			typeof(IPointerInteractable)
		);
	}


	private void ManageCurve(double delta)
	{
		if (GetNode("BezierCurve3D") != null)
		{
			BezierCurve3D curve = (BezierCurve3D)GetNode("BezierCurve3D");
			float distanceMultiplier = Mathf.InverseLerp(0.0f, _defaultDistance, TargetPosition.Z); 
			Vector3 velOffset = _controller.GetLocalVelocity() * _velocityStrength * Mathf.Abs(distanceMultiplier); 

			curve.EndPoint = TargetPosition * Mathf.InverseLerp(0, 100, _visiblePercent);
			curve.MidPoint.Z = TargetPosition.Z / 2;
			curve.MidPoint = (curve.StartPoint + curve.EndPoint) / 2 + velOffset; 
		}

		if (GetPointerInteractable() != null) 
		{
			Vector3 target = Vector3.Zero; 
			target.Z = -(GlobalPosition.DistanceTo(GetCollisionPoint()) + 0.05f); 
			TargetPosition = target; 
			Visible = true; 
		} 
		else 
		{ 
			Vector3 target = Vector3.Zero; 
			target.Z = -_defaultDistance; 
			TargetPosition = target; 
			Visible = false; 
		}
	}


	public void OnGrabbed(Interactable interactable)
	{ 
		if (_disableWhenGrabbing) 
		{
			Disabled = true; 
			Visible = false; 
		}
	}


	public void OnDropped(Interactable interactable)
	{ 
		if (_disableWhenGrabbing) 
		{
			Disabled = false; 
		}
	}
}

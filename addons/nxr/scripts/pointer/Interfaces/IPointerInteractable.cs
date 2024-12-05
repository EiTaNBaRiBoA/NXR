using Godot;
using System;

public interface IPointerInteractable 
{ 
	public Pointer CurrentPointer { get; set; }
	
	public void PointerEntered(); 
	public void PointerExited(); 
	public void Pressed(Vector3 where); 	
	public void Released(Vector3 where); 	
	public void Moved(Vector3 where); 	
}
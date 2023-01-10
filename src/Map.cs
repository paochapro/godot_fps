using Godot;
using System;

public class Map : Spatial
{
    public override void _Ready()
    {
        var ui = GetNode("/root/World/Control") as UI;
        ui.MapReady(this);
    }
}
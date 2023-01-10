using Godot;
using System;

public class Ladder : Area
{
    public override void _Ready()
    {
        
    }

    public void OnActorEntered(Node node)
    {
        Actor actor = node as Actor;
        actor.OnLadderEnter(this);
    }

    public void OnActorLeft(Node node)
    {
        Actor actor = node as Actor;
        actor.OnLadderLeft(this);
    }
}

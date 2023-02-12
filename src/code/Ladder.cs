using Godot;
using System;

public class Ladder : Area
{
    public void OnActorEntered(Node node)
    {
        Player ply = node as Player;
        //ply.OnLadderEnter(this);
    }

    public void OnActorLeft(Node node)
    {
        Player ply = node as Player;
        //ply.OnLadderLeft(this);
    }
}

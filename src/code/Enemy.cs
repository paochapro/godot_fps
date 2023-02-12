using Godot;
using System;

public class Enemy : KinematicBody
{
    private const float speed = 2000f;

    //Nodes
    private Player player;
    private NavigationAgent navAgent;

    private Vector3 velocity;

    public override void _Ready()
    {
        var map = GetParent();
        navAgent = GetNode<NavigationAgent>("NavigationAgent");
        player = map.GetNode<Player>("Player");
        UpdateNav();
    }

    public override void _Process(float dt)
    {
        Navigate(dt);
        velocity = MoveAndSlide(velocity);
    }

    private void Navigate(float dt)
    {
        if(!navAgent.IsNavigationFinished())
        {
            Vector3 next = navAgent.GetNextLocation();
            Vector3 dir = GlobalTranslation.DirectionTo(next);
            velocity = dir * speed * dt;
            navAgent.SetVelocity(velocity);
        }
        else
            velocity = Vector3.Zero;
    }

    private void UpdateNav()
    {
        navAgent.SetTargetLocation(player.GlobalTranslation);
    }
}

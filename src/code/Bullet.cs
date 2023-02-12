using Godot;
using System;

class Bullet : Area
{
    private float speed;
    private Vector3 dir;

    public void Init(Vector3 origin, float speed, Vector3 dir, uint collisionLayer, uint collisionMask) 
    {
        Translation = origin;
        this.speed = speed;
        this.dir = dir;
        CollisionLayer = collisionLayer;
        CollisionMask = collisionMask;
    }

    public override void _Ready() {}

    public override void _Process(float dt)
    {
        Translate(dir * speed * dt);
    }

    private void HitBody(PhysicsBody body)
    {
        QueueFree();
    }
}

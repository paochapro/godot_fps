using Godot;
using System;

public class Explosion : Spatial
{
    private const float PEEK_SCALE = 20f;
    private const float SHRINK_SPEED = 120f;
    private const float EXPANSION_SPEED = 80f; 
    private const float EXPLOSION_RADIUS = 20f;

    private float currentScale;
    private bool isShrinking;
    private Sprite3D sprite;
    private Map map;

    public override void _Ready()
    {
        sprite = GetNode<Sprite3D>("Sprite3D");
        map = GetNode<Map>("/root/World/Map");

        currentScale = 0f;
        isShrinking = false;

        UpdateScale(0f);
        ExplosionDamage();
    }

    public override void _Process(float dt)
    {
        if(isShrinking)
        {
            currentScale -= SHRINK_SPEED * dt;
            if(currentScale <= 0f) {
                QueueFree();
                return;
            }
        }
        else
        {
            currentScale += EXPANSION_SPEED * dt;
            if(currentScale >= PEEK_SCALE) {
                currentScale = PEEK_SCALE;
                isShrinking = true;
            }
        }

        UpdateScale(currentScale);
    }

    private void UpdateScale(float scale)
    {
        sprite.Scale = new Vector3(scale, scale, scale);
    }

    private void ExplosionDamage()
    {
        var ss = map.GetWorld().DirectSpaceState;

        var sphereShape = new SphereShape();
        var physicsShape = new PhysicsShapeQueryParameters();
        sphereShape.Radius = EXPLOSION_RADIUS;
        physicsShape.SetShape(sphereShape);

        var newTransform = physicsShape.Transform;
        newTransform.origin = this.Translation;
        physicsShape.Transform = newTransform;

        //Result
        var hitObjects = ss.IntersectShape(physicsShape);

        foreach(Godot.Collections.Dictionary hitObject in hitObjects)
        {
            Spatial collider = (Spatial)hitObject["collider"];

            var distance = physicsShape.Transform.origin.DistanceTo(collider.Translation);
            float damage = Mathf.RangeLerp(distance, EXPLOSION_RADIUS, 0, 0, 100);

            if(collider is Actor actor)
                actor.OnExplosionHit(damage);
        }
    }
}
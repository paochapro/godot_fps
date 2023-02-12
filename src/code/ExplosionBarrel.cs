using Godot;
using System;

public class ExplosionBarrel : StaticBody, IHitable
{
    //Nodes
    private Map map;
    private float health;

    public override void _Ready() {
        map = GetNode<Map>("/root/World/Map");
        health = 2;
    }

    public void BulletEntered(Node body)  
    {
        GD.Print("bullet entered");
        Damage();
    }

    public void OnHitscanHit()
    {
        GD.Print("hitscan hit");
        Damage();
    }

    private void Damage()
    {
        health -= 1;
        if(health <= 0)
            Explode();
    }
    
    public void Explode() 
    {
        GD.Print("explode");

        //Create a explosion sprite
        var explosionSpriteScene = GD.Load<PackedScene>("res://src/tscn/Explosion.tscn");
        var explosionSprite = explosionSpriteScene.Instance() as Explosion;
        explosionSprite.Translation = this.Translation;
        map.AddChild(explosionSprite);

        QueueFree();
    }
}

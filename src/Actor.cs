using Godot;

abstract class Actor : KinematicBody, IHitable
{
    public abstract void OnHitscanHit();
    public abstract void OnProjectileHit();

    public virtual void OnExplosionHit(float damage)
    {
        GD.Print("Explosion barrel dmg: " + damage);
    }
}
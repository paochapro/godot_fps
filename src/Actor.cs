using Godot;

abstract class Actor : KinematicBody, IHitable
{
    protected bool OnLadder => Ladder != null;
    protected Ladder Ladder { get; private set; }

    public abstract void OnHitscanHit();
    public abstract void OnProjectileHit();

    public virtual void OnExplosionHit(float damage)
    {
        GD.Print("Explosion barrel dmg: " + damage);
    }

    public void OnLadderEnter(Ladder ladder) => Ladder = ladder;
    public void OnLadderLeft(Ladder ladder) => GetOffLadder();
    protected void GetOffLadder() => Ladder = null;
}
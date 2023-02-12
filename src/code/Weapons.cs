using Godot;
using System;

abstract class Weapon
{
    protected readonly uint collisionLayer = 8;
    protected readonly uint collisionMask = 1;

    private int ammo;
    private int magazine;
    private readonly int magazineCapacity;

    public int Ammo => ammo;
    public int Magazine => magazine;

    public PackedScene WeaponModel { get; private set; }
    public bool CanReload => magazine != magazineCapacity && ammo != 0;

    public Weapon(int startAmmo, int magazineCapacity ,PackedScene weaponModel)
    {
        this.WeaponModel = weaponModel;
        this.magazineCapacity = magazineCapacity;

        magazine = magazineCapacity;
        ammo = startAmmo;
    }

    public bool Reload()
    {
        int neededMagazine = magazineCapacity - magazine;
        int beforeAmmo = ammo;
        int afterAmmo = ammo - neededMagazine;

        if(afterAmmo < 0) afterAmmo = 0;

        ammo = afterAmmo;
        magazine += beforeAmmo - afterAmmo;

        return ammo != 0;
    }

    public bool TryShoot(Vector3 origin, Vector3 dir, Spatial map)
    {
        if(magazine <= 0) return false;

        FireOutput(origin, dir, map);
        magazine -= 1;

        return true;
    }

    protected abstract void FireOutput(Vector3 origin, Vector3 dir, Spatial map);
}

abstract class WeaponProjectile : Weapon
{
    protected readonly float speed = 0f;
    private int startAmmo;

    public WeaponProjectile(int startAmmo, float speed, PackedScene weaponModel) 
        : base(startAmmo, 5, weaponModel)
    {
        this.speed = speed;
    }

    protected override void FireOutput(Vector3 origin, Vector3 dir, Spatial map)
    {
        PackedScene bulletScene = GD.Load<PackedScene>("res://src/tscn/Bullet.tscn");
        Bullet bullet = bulletScene.Instance() as Bullet;
        bullet.Init(origin, speed, dir, collisionLayer, collisionMask);
        map.AddChild(bullet);
    }
}

abstract class WeaponHitscan : Weapon
{
    private const float RAY_DISTANCE = 600f;
    private static readonly Texture bulletHoleTexture;

    static WeaponHitscan()
    {
        bulletHoleTexture = GD.Load<Texture>("res://content/resources/bullet_hole.png");
    }

    public WeaponHitscan(int startAmmo, PackedScene weaponModel)
        : base(startAmmo, 5, weaponModel)
    {
    }

    protected override void FireOutput(Vector3 origin, Vector3 dir, Spatial map)
    {
        var ss = map.GetWorld().DirectSpaceState;
        Vector3 from = origin;
        Vector3 to = origin + dir * RAY_DISTANCE;
        var rayResult = ss.IntersectRay(from, to, null, collisionMask, true, true);

        if(rayResult.Keys.Count != 0)
        {
            var collider = rayResult["collider"] as CollisionObject;

            if(collider is CollisionObject obj)
            {
                var pos = (Vector3)rayResult["position"];
                var normal = (Vector3)rayResult["normal"];

                obj.PlaceBulletHoleDecal(pos, normal, dir, bulletHoleTexture);

                if(obj is IHitable hitable)
                    hitable.OnHitscanHit();
            }
        }
    }
}

class WeaponProjectilePistol : WeaponProjectile 
{
    private const float pistol_speed = 50f;
    private const int startAmmo = 30;

    public WeaponProjectilePistol()
        : base(startAmmo, pistol_speed, GD.Load<PackedScene>("res://content/resources/gun.glb"))
    {
    }
}

class WeaponHitscanPistol : WeaponHitscan 
{
    private const int startAmmo = 30;

    public WeaponHitscanPistol()
        : base(startAmmo, GD.Load<PackedScene>("res://content/resources/gun.glb"))
    {
    }
}
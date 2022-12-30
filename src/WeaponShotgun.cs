using Godot;

class WeaponShotgun : WeaponHitscan
{
    private const float bulletsMaxOffset = 0.1f;
    private const int startAmmo = 10;

    protected override void FireOutput(Vector3 origin, Vector3 dir, Spatial map)
    {
        Vector3 bulletDirection = dir;

        for(int i = 0; i < 9; ++i)
        {
            bulletDirection.x += (float)GD.RandRange(-bulletsMaxOffset, bulletsMaxOffset);
            bulletDirection.y += (float)GD.RandRange(-bulletsMaxOffset, bulletsMaxOffset);
            bulletDirection.z += (float)GD.RandRange(-bulletsMaxOffset, bulletsMaxOffset);
            base.FireOutput(origin, bulletDirection, map);
            bulletDirection = dir;
        }
    }

    public WeaponShotgun()
        : base(startAmmo, GD.Load<PackedScene>("res://content/shotgun.glb"))
    {
    }
}
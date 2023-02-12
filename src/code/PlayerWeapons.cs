using Godot;
using System.Collections.Generic;
using System.Linq;

class PlayerWeaponManager : Node
{
    const float reloadWaitTime = 1f;
    List<Weapon> weapons;
	Weapon currentWeapon;
    Timer reloadTimer;
    Spatial weaponPivot;
    Map map;

    public Weapon CurrentWeapon => currentWeapon;
    public float TimeLeftToReload => reloadTimer.TimeLeft;

    public PlayerWeaponManager(IEnumerable<Weapon> startWeapons, Map map, Spatial weaponPivot)
    {
        weapons = startWeapons.ToList();
        this.weaponPivot = weaponPivot;
        this.map = map;

        reloadTimer = new Timer();
        reloadTimer.OneShot = true;
        reloadTimer.WaitTime = reloadWaitTime;
        AddChild(reloadTimer);
        reloadTimer.Connect("timeout", this, "ReloadWeapon");
    }

    public bool TryShoot(Vector3 from, Vector3 dir)
	{
		bool shot = currentWeapon.TryShoot(from, dir, map);

		if(shot)
		{
			var weaponModel = weaponPivot.GetChild<Spatial>(0);
			var animPlayer = weaponModel.GetNode<AnimationPlayer>("AnimationPlayer");
			animPlayer.Stop();
			animPlayer.Play("shoot");
		}

        return shot;
	}

	public bool TryReload()
	{
        if(reloadTimer.IsStopped() && currentWeapon.CanReload) {
            reloadTimer.Start();
            return true;
        }

        return false;
	}

    void ReloadWeapon() => currentWeapon.Reload(); 

	public bool TrySwitchWeapon(int weaponSlot)
	{
		if(weaponSlot >= weapons.Count) 
            return false;

		currentWeapon = weapons[weaponSlot];
		reloadTimer.Stop();

		//Change weapon model
		foreach(Node node in weaponPivot.GetChildren())
			node.QueueFree();

		weaponPivot.AddChild(currentWeapon.WeaponModel.Instance());
        return true;
	}
}
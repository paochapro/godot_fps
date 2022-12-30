using Godot;
using System;
using System.Collections.Generic;

class Player : Actor
{
	//Constants
	// private const float GRAVITY = 70f;
	// private const float MAX_FALLING_SPEED = 100f;
	// private const float JUMP_FORCE = 20f;
	// private const float MAX_SPEED = 30f;
	// private const float ACC = 300f;
	// private const float FRICTION = 0.75f;

	private float GRAVITY = 70f;
	private float MAX_FALLING_SPEED = 100f;
	private float JUMP_FORCE = 22f;
	private float MAX_SPEED = 30f;
	private float ACC = 15f;
	private float FRICTION = 10f;
	private float AIR_ACC = 2f;
	private float AIR_FRICTION = 1f;

	private float sensetivity = 0.0017f;
	private readonly float FLOOR_MAX_ANGLE = Mathf.Deg2Rad(45);
	private readonly float MIN_YAW = Mathf.Deg2Rad(-90);
	private readonly float MAX_YAW = Mathf.Deg2Rad(90);

	private Vector3 velocity;
	private float fall;
	private bool justJumped;
	private Vector3 movingDir;
	private List<Weapon> weapons;

	//Nodes
	private Camera camera;
	private Weapon currentWeapon;
	private Spatial map;
	private Control ui;
	private Timer reloadTimer;

	public override void _Ready() {

		//Getting nodes
		camera = GetNode<Camera>("Camera");
		reloadTimer = GetNode<Timer>("ReloadTimer");
		map = GetNode<Map>("/root/World/Map");
		ui = GetNode<Control>("/root/World/Control");

		//Weapon setup
		weapons = new List<Weapon>();
		weapons.Add(new WeaponHitscanPistol());
		weapons.Add(new WeaponShotgun());

		SwitchWeapon(0);

		//Connecting top left settings with our properties
		var settings = GetNode("/root/World/Control/settings");
		((SpinBox)settings.GetNode("gravity/gravity")).Value 								= GRAVITY;
		((SpinBox)settings.GetNode("max_falling_speed/max_falling_speed")).Value 			= MAX_FALLING_SPEED;
		((SpinBox)settings.GetNode("jump_force/jump_force")).Value 							= JUMP_FORCE;
		((SpinBox)settings.GetNode("max_speed/max_speed")).Value 							= MAX_SPEED;
		((SpinBox)settings.GetNode("acc/acc")).Value 										= ACC;
		((SpinBox)settings.GetNode("sens/sens")).Value 										= sensetivity;
	}

	public override void _Process(float dt) 
	{
		Controls(dt);

		//Updating ui
		var ammoLabel = ui.GetNode<Label>("AmmoRect/AmmoLabel");
		ammoLabel.Text = currentWeapon.Magazine + "/" + currentWeapon.Ammo;
	}

	public override void _PhysicsProcess(float dt)
	{
		fall = Mathf.MoveToward(fall, -MAX_FALLING_SPEED, GRAVITY * dt);

		//Snapping
		Vector3 snap = Vector3.Zero;
		if(IsOnFloor())
		{
			if(justJumped)
				snap = Vector3.Zero;
			else
			{
				snap = -GetFloorNormal();
				fall = 0;
			}
		}
		else
			snap = Vector3.Down;

		//Horizontal velocity
		float acc = 0;

		if(movingDir != Vector3.Zero)
			acc = IsOnFloor() ? ACC : AIR_ACC;
		else
			acc = IsOnFloor() ? FRICTION : AIR_FRICTION;

		velocity = velocity.LinearInterpolate(movingDir * MAX_SPEED, acc * dt);

		//Moving
		Vector3 finalVec = new Vector3(velocity.x, fall, velocity.z);
		Vector3 result = MoveAndSlideWithSnap(finalVec, snap, Vector3.Up);

		if(IsOnWall())
			velocity = result;
	}

	public override void _Input(InputEvent ev)
	{
		if(ev is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
			Looking(mouseMotion.Relative);
	}

	private void Controls(float dt)
	{
		movingDir = Walking(dt);

		if(justJumped) justJumped = false;

		if(IsOnFloor() && Input.IsActionJustPressed("jump"))
			Jump();

		if(Input.IsActionJustPressed("ui_cancel"))
		{
			if(Input.MouseMode == Input.MouseModeEnum.Visible)
				Input.MouseMode = Input.MouseModeEnum.Captured;
			else
				Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		if(Input.IsActionJustPressed("fire"))
			currentWeapon.Shoot(camera.GlobalTranslation, -camera.Transform.basis.z, map);

		if(Input.IsActionJustPressed("reload"))
			reloadTimer.Start();

		for(int i = 0; i < 3; i++)
			if(Input.IsActionJustPressed("switch_weapon_" + i))
				SwitchWeapon(i);

		GetNode<Label>("/root/World/Control/reloadTimer").Text = reloadTimer.TimeLeft.ToString();
	}

	private void Reload()
	{
		currentWeapon.Reload();
	}

	private Vector3 Walking(float dt)
	{
		Vector3 camRot = camera.Rotation;
		Vector3 move = Vector3.Zero;

		if(Input.IsActionPressed("go_forward"))
			move += new Vector3(-Mathf.Sin(camRot.y), 0, -Mathf.Cos(camRot.y));

		if(Input.IsActionPressed("go_backward"))
			move += new Vector3(Mathf.Sin(camRot.y), 0, Mathf.Cos(camRot.y));

		if(Input.IsActionPressed("go_left"))
			move += new Vector3(-Mathf.Cos(camRot.y), 0, Mathf.Sin(camRot.y));

		if(Input.IsActionPressed("go_right"))
			move += new Vector3(Mathf.Cos(camRot.y), 0, -Mathf.Sin(camRot.y));

		return move.Normalized();
	}

	private void Jump()
	{
		justJumped = true; 
		fall = JUMP_FORCE;
	}

	private void Looking(Vector2 relative)
	{
		Vector3 newRot = camera.Rotation;
		newRot.y -= relative.x * sensetivity;
		newRot.x -= relative.y * sensetivity;
		newRot.x = Mathf.Clamp(newRot.x, MIN_YAW, MAX_YAW);
		camera.Rotation = newRot;
	}

	public override	void OnHitscanHit()
	{

	}

	public override void OnProjectileHit()
	{
		
	}

	private void SwitchWeapon(int weaponSlot)
	{
		if(weaponSlot >= weapons.Count) return;

		currentWeapon = weapons[weaponSlot];

		reloadTimer.Stop();

		//Change weapon model
		var weaponOrigin = GetNode("Camera/WeaponOrigin");

		foreach(Node node in weaponOrigin.GetChildren())
			node.QueueFree();

		weaponOrigin.AddChild(currentWeapon.WeaponModel.Instance());
	}

	public void _on_acc_value_changed(float val) => ACC = val;
	public void _on_max_speed_value_changed(float val) => MAX_SPEED = val;
	public void _on_gravity_value_changed(float val) => GRAVITY = val;
	public void _on_jump_force_value_changed(float val) => JUMP_FORCE = val; 
	public void _on_max_falling_speed_value_changed(float val) => MAX_FALLING_SPEED = val;
	public void _on_sens_value_changed(float val) => sensetivity = val; 
}
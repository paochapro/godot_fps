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
	// private const float FLOOR_ACC = 300f;
	// private const float FLOOR_FRICTION = 0.75f;

	private float GRAVITY = 70f;
	private float MAX_FALLING_SPEED = 100f;
	private float JUMP_FORCE = 22f;
	private float MAX_SPEED = 25f;
	private float FLOOR_ACC = 80f;
	private float FLOOR_FRICTION = 0.9f;
	private float AIR_ACC = 50f;
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
	private Spatial weaponOrigin;
	private Vector3 spawnPos;

	public override void _Ready() {

		//Getting nodes
		camera = GetNode<Camera>("Camera");
		reloadTimer = GetNode<Timer>("ReloadTimer");
		weaponOrigin = GetNode<Spatial>("Camera/WeaponOrigin");
		map = GetNode<Map>("/root/World/Map");
		ui = GetNode<Control>("/root/World/Control");

		//Weapon setup
		weapons = new List<Weapon>();
		weapons.Add(new WeaponHitscanPistol());
		weapons.Add(new WeaponShotgun());

		SwitchWeapon(0);

		//Connecting top left settings with our properties
		var movementVarNames = new Dictionary<string, float>() {
			["gravity"] 				= GRAVITY,
			["max_falling_speed"] 		= MAX_FALLING_SPEED,
			["jump_force"] 				= JUMP_FORCE,
			["max_speed"] 				= MAX_SPEED,
			["sens"] 					= sensetivity,
			["floor_acc"] 				= FLOOR_ACC,
			["floor_friction"] 			= FLOOR_FRICTION,
			["air_acc"] 				= AIR_ACC,
			["air_friction"] 			= AIR_FRICTION,
		};

		var settings = GetNode("/root/World/Control/settings");
		foreach(Container container in settings.GetChildren())
		{
			SpinBox spinBox = container.GetChild<SpinBox>(1);
			spinBox.Value = movementVarNames[container.Name];
		}

		//Storing spawn position
		spawnPos = Translation;
	}

	public override void _Process(float dt) 
	{
		Controls(dt);

		if(Translation.y <= -100)
		{
			velocity = Vector3.Zero;
			fall = 0;
			Translation = spawnPos;
		}

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
		Vector3 go = movingDir * MAX_SPEED;

		if(movingDir != Vector3.Zero)
		{
			float acc = (IsOnFloor() ? FLOOR_ACC : AIR_ACC) * dt;
			velocity = velocity.MoveToward(go, acc);
		}
		else
		{
			float friction = IsOnFloor() ? FLOOR_FRICTION : AIR_FRICTION; //no dt
			velocity = Vector3.Zero.LinearInterpolate(velocity, friction);
		}

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
		{
			bool shot = currentWeapon.Shoot(camera.GlobalTranslation, -camera.Transform.basis.z, map);

			if(shot)
			{
				var weaponModel = weaponOrigin.GetChild<Spatial>(0);
				var animPlayer = weaponModel.GetNode<AnimationPlayer>("AnimationPlayer");
				animPlayer.Play("shoot");
			}
		}

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
		foreach(Node node in weaponOrigin.GetChildren())
			node.QueueFree();

		weaponOrigin.AddChild(currentWeapon.WeaponModel.Instance());
	}

	public void _on_floor_acc_value_changed(float val) => FLOOR_ACC = val;
	public void _on_floor_friction_value_changed(float val) => FLOOR_FRICTION = val;
	public void _on_air_acc_value_changed(float val) => AIR_ACC = val;
	public void _on_air_friction_value_changed(float val) => AIR_FRICTION = val;
	public void _on_max_speed_value_changed(float val) => MAX_SPEED = val;
	public void _on_gravity_value_changed(float val) => GRAVITY = val;
	public void _on_jump_force_value_changed(float val) => JUMP_FORCE = val; 
	public void _on_max_falling_speed_value_changed(float val) => MAX_FALLING_SPEED = val;
	public void _on_sens_value_changed(float val) => sensetivity = val;
}
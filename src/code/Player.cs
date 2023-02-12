using Godot;
using System;
using System.Collections.Generic;

class Player : Actor
{
	float sensetivity = 0.0017f;

	readonly float FLOOR_MAX_ANGLE = Mathf.Deg2Rad(45);
	readonly float MIN_YAW = Mathf.Deg2Rad(-90);
	readonly float MAX_YAW = Mathf.Deg2Rad(90);
	readonly Vector3 CROUCH_SIZE = new Vector3(1,1.5f,1);
	readonly Vector3 FULL_SIZE = new Vector3(1,2,1);

	Vector3 movingDir;
	bool isCrouched;
	bool isJumping;

	Dictionary<string, object> debugVars = new Dictionary<string, object>();
	PlayerMovement pm;
	PlayerWeaponManager wm;
	Vector3 spawnPos;

	//Nodes
	Camera camera;
	CollisionShape collisionShape;
	Control ui;
	Map map;

	public override void _Ready() {

		//Getting nodes
		camera = GetNode<Camera>("Camera");
		map = GetNode<Map>("/root/World/Map");
		ui = GetNode<Control>("/root/World/Control");
		collisionShape = GetNode<CollisionShape>("CollisionShape");

		pm = new PlayerMovement(debugVars);

		//Weapon setup
		Spatial weaponPivot = GetNode<Spatial>("Camera/WeaponOrigin");	
		Weapon[] startWeapons = new Weapon[] { new WeaponHitscanPistol(), new WeaponShotgun() };
		wm = new PlayerWeaponManager(startWeapons, map, weaponPivot);
		AddChild(wm);
		wm.TrySwitchWeapon(0);

		var settings = GetNode("/root/World/Control/settings");
		CreateMovementProperties(settings);

		//Camera rotation
		LineEdit camRotLineEdit = settings.GetNode("camrot").GetNode("LineEdit") as LineEdit;
		camRotLineEdit.Connect("text_changed", this, "ChangeCameraRotation");

		//Storing spawn position
		spawnPos = Translation;
	}
	
	public override void _Process(float dt) 
	{
		Controls(dt);

		if(Translation.y <= -100)
			Death();

		//Updating ui
		var ammoLabel = ui.GetNode<Label>("AmmoRect/AmmoLabel");
		ammoLabel.Text = wm.CurrentWeapon.Magazine + "/" + wm.CurrentWeapon.Ammo;
	}

	public override void _PhysicsProcess(float dt)
	{
		PlayerInfo info = new PlayerInfo() {
			IsOnWall = IsOnWall(),
			IsOnCeiling = IsOnCeiling(),
			IsOnFloor = IsOnFloor(),
			FloorNormal = GetFloorNormal(),
			MovingDir = movingDir,
			IsJumping = isJumping,
			IsCrouched = isCrouched,
			FloorMaxAngle = FLOOR_MAX_ANGLE
		};

		Vector3 velocity = pm.Process(dt, info);
		Vector3 movingResult = MoveAndSlideWithSnap(velocity, pm.Snap, Vector3.Up);
		pm.PostProcess(dt, info, movingResult, GetCollisions());

		DebugVarsProcess();
		isJumping = false;
	}

	public override void _Input(InputEvent ev)
	{
		if(ev is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
			Looking(mouseMotion.Relative);

		if(ev is InputEventMouseButton mouseEv)
			if(mouseEv.IsPressed())
				if(mouseEv.ButtonIndex == (int)ButtonList.WheelDown)
					isJumping = true;
	}

	public override	void OnHitscanHit() {}

	public override void OnProjectileHit() {}

	void Controls(float dt)
	{
		movingDir = Walking(dt);

		if(Input.IsActionJustPressed("jump"))
			isJumping = true;

		if(Input.IsActionJustPressed("crouch"))
			SetCrouch(true);

		if(!Input.IsActionPressed("crouch"))
			SetCrouch(false);

		if(Input.IsActionJustPressed("ui_cancel"))
		{
			if(Input.MouseMode == Input.MouseModeEnum.Visible)
				Input.MouseMode = Input.MouseModeEnum.Captured;
			else
				Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		if(Input.IsActionJustPressed("shoot"))
			wm.TryShoot(camera.GlobalTranslation, -camera.Transform.basis.z);

		if(Input.IsActionJustPressed("reload"))
		{
			bool can = wm.TryReload();
			debugVars.AddOrSet("tryReloadingSucceded", can);
		}

		for(int i = 0; i < 3; i++)
			if(Input.IsActionJustPressed("switch_weapon_" + i))
				wm.TrySwitchWeapon(i);

		var reloadTimeLeftLabel = GetNode<Label>("/root/World/Control/reloadTimer");
		reloadTimeLeftLabel.Text = wm.TimeLeftToReload.ToString();
	}

	Vector3 Walking(float dt)
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

	void Looking(Vector2 relative)
	{
		Vector3 newRot = camera.Rotation;
		newRot.y -= relative.x * sensetivity;
		newRot.x -= relative.y * sensetivity;
		newRot.x = Mathf.Clamp(newRot.x, MIN_YAW, MAX_YAW);
		camera.Rotation = newRot;
	}

	void SetCrouch(bool crouch)
	{
		if(isCrouched == crouch) return;

		Vector3 fromSize = collisionShape.Scale;
		Vector3 toSize = crouch ? CROUCH_SIZE : FULL_SIZE;
		Vector3 translation = toSize - fromSize;

		//Is uncrouching possible (is there a ceiling blocking us)
		if(!crouch && TestMove(Transform, translation))
			return;
		
		collisionShape.Scale = toSize;

		Translate(translation);
		camera.GlobalTranslate(translation);

		isCrouched = crouch;
	}

	void Death()
	{
		pm = new PlayerMovement(debugVars);
		Translation = spawnPos;
	}

	KinematicCollision[] GetCollisions()
	{
		//Getting collisions
		int collisionsCount = GetSlideCount();
		var collisions = new KinematicCollision[collisionsCount];

		for(int i = 0; i < collisions.Length; ++i)
			collisions[i] = GetSlideCollision(i);

		return collisions;
	}

	void DebugVarsProcess()
	{
		var debugVarsContainer = GetNode<VBoxContainer>("/root/World/Control/vars");

		foreach(KeyValuePair<string, object> pair in debugVars)
		{
			Label label = debugVarsContainer.GetNodeOrNull<Label>(pair.Key);
			 
			if(label == null) {
				label = new Label();
				label.Name = pair.Key;
				debugVarsContainer.AddChild(label);
			}

			label.Text = pair.Key + ": " + pair.Value;
		}
	}

	void CreateMovementProperties(Node settings)
	{
		//Create properties in settings
		// var movementVars = new Dictionary<string, float>() {
		// 	["gravity"] 				= GRAVITY,
		// 	["max_falling_speed"] 		= MAX_FALLING_SPEED,
		// 	["jump_force"] 				= JUMP_FORCE,
		// 	["air_friction"] 			= AIR_FRICTION,
		// 	["floor_friction"]			= FLOOR_FRICTION,
		// 	["sens"] 					= sensetivity,
		// 	["full_max_speed"]			= FULL_MAX_SPEED,
		// 	["crouch_max_speed"]		= CROUCH_MAX_SPEED,
		// 	["full_air_acc"]			= FULL_AIR_ACC,
		// 	["full_floor_acc"]			= FULL_FLOOR_ACC,
		// 	["crouch_air_acc"]			= CROUCH_AIR_ACC,
		// 	["crouch_floor_acc"]		= CROUCH_FLOOR_ACC,
		// };

		var movementVars = new Dictionary<string, float>();

		//Add settings based on movementVars
		foreach(string movementVarName in movementVars.Keys)
		{
			HBoxContainer container = new HBoxContainer();

			Label label = new Label();
			SpinBox spinBox = new SpinBox();
			label.Text = movementVarName;
			spinBox.Step = 0;
			spinBox.AllowGreater = true;
			spinBox.AllowLesser = true;
			spinBox.Value = movementVars[movementVarName];

			spinBox.Connect("value_changed", this, "_on_" + movementVarName + "_value_changed");

			container.AddChild(label);
			container.AddChild(spinBox);
			settings.AddChild(container);
		}

	}

	void ChangeCameraRotation(string str)
	{
		var values = str.Split(",");

		try
		{
			float x = float.Parse(values[0]);
			float y = float.Parse(values[1]);
			float z = float.Parse(values[2]);
			camera.Rotation = new Vector3(Mathf.Deg2Rad(x), Mathf.Deg2Rad(y), Mathf.Deg2Rad(z));
		}
		catch
		{
			GD.Print("Couldn't parse this string: " + str);
		}
	}
}

static class DictionaryExtensions
{
	static public void AddOrSet<K,V>(this Dictionary<K,V> dictionary, K key, V value)
	{
		if(!dictionary.ContainsKey(key))
			dictionary.Add(key, value);
		else
			dictionary[key] = value;
	}
}
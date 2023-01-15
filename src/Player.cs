using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

class Player : Actor
{
	//Constants
	float GRAVITY = 70f;
	float MAX_FALLING_SPEED = 100f;
	float JUMP_FORCE = 22f;
	float sensetivity = 0.0017f;

	//Horinzontal movement
	float AIR_FRICTION = 1f;
	float FLOOR_FRICTION = 0.85f;

	float FULL_MAX_SPEED = 25f;
	float CROUCH_MAX_SPEED = 10f;

	//Raised by 130%
	const float ACC_PERCENT_RAISE = 130f;
	float FULL_FLOOR_ACC 		= 130f + (130f/100f * ACC_PERCENT_RAISE);
	float FULL_AIR_ACC 			= 10f + (10f/100f * ACC_PERCENT_RAISE);
	float CROUCH_FLOOR_ACC 		= 30f + (30f/100f * ACC_PERCENT_RAISE);
	float CROUCH_AIR_ACC 		= 20f + (20f/100f * ACC_PERCENT_RAISE);

	const float LADDER_MOVEMENT_SPEED = 25f;
	readonly Vector3 ladderJumpOffForce = new Vector3(60f, 0f, 60f);

	readonly float FLOOR_MAX_ANGLE = Mathf.Deg2Rad(45);
	readonly float MIN_YAW = Mathf.Deg2Rad(-90);
	readonly float MAX_YAW = Mathf.Deg2Rad(90);
	readonly Vector3 CROUCH_SIZE = new Vector3(1,1.5f,1);
	readonly Vector3 FULL_SIZE = new Vector3(1,2,1);

	Vector3 velocity;
	float fall;
	bool justJumped;
	Vector3 movingDir;
	List<Weapon> weapons;
	bool isCrouched;

	Dictionary<string, Action<float>> additionalMovementVars;

	//Movetype
	enum MoveType {
		Ground,
		Ladder,
		Water
	}
	Dictionary<MoveType, Func<float, Vector3>> movingFunctions;
	MoveType moveType;

	Dictionary<string, object> debugVars = new Dictionary<string, object>();
	// 	["globalAddSpeed"] = 0f,
	// 	["globalCurrentSpeed"] = 0f,
	// 	["globalSurpass"] = false,
	// };

	//Nodes
	Camera camera;
	Weapon currentWeapon;
	Spatial map;
	Control ui;
	Timer reloadTimer;
	Spatial weaponOrigin;
	Vector3 spawnPos;
	CollisionShape collisionShape;

	public override void _Ready() {

		//Getting nodes
		camera = GetNode<Camera>("Camera");
		reloadTimer = GetNode<Timer>("ReloadTimer");
		weaponOrigin = GetNode<Spatial>("Camera/WeaponOrigin");	
		map = GetNode<Map>("/root/World/Map");
		ui = GetNode<Control>("/root/World/Control");
		collisionShape = GetNode<CollisionShape>("CollisionShape");

		moveType = MoveType.Ground;

		movingFunctions = new Dictionary<MoveType, Func<float, Vector3>> {
			[MoveType.Ground] = GroundMovement,
			[MoveType.Ladder] = LadderMovement,
			[MoveType.Water] = WaterMovement,
		};

		//Weapon setup
		weapons = new List<Weapon>();
		weapons.Add(new WeaponHitscanPistol());
		weapons.Add(new WeaponShotgun());

		//Debug vars

		SwitchWeapon(0);

		//Create properties in settings
		var movementVars = new Dictionary<string, float>() {
			["gravity"] 				= GRAVITY,
			["max_falling_speed"] 		= MAX_FALLING_SPEED,
			["jump_force"] 				= JUMP_FORCE,
			["air_friction"] 			= AIR_FRICTION,
			["floor_friction"]			= FLOOR_FRICTION,
			["sens"] 					= sensetivity,
			["full_max_speed"]			= FULL_MAX_SPEED,
			["crouch_max_speed"]		= CROUCH_MAX_SPEED,
			["full_air_acc"]			= FULL_AIR_ACC,
			["full_floor_acc"]			= FULL_FLOOR_ACC,
			["crouch_air_acc"]			= CROUCH_AIR_ACC,
			["crouch_floor_acc"]		= CROUCH_FLOOR_ACC,
		};

		var settings = GetNode("/root/World/Control/settings");
			
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

		//Camera rotation
		LineEdit camRotLineEdit = settings.GetNode("camrot").GetNode("LineEdit") as LineEdit;
		camRotLineEdit.Connect("text_changed", this, "ChangeCameraRotation");

		//Storing spawn position
		spawnPos = Translation;
	}

	private void ChangeCameraRotation(string str)
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
	
	public override void _Process(float dt) 
	{
		Controls(dt);

		if(Translation.y <= -100)
			Death();

		//Updating ui
		var ammoLabel = ui.GetNode<Label>("AmmoRect/AmmoLabel");
		ammoLabel.Text = currentWeapon.Magazine + "/" + currentWeapon.Ammo;
	}

	public override void _PhysicsProcess(float dt)
	{
		Vector3 result = movingFunctions[moveType].Invoke(dt);

		// TODO: Makes velocity go over maxSpeed 
		// if(IsOnWall())
		// 	velocity = result;

		debugVars.AddOrSet("globalCurrentSpeed", velocity.Length());

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

	private Vector3 GroundMovement(float dt)
	{
		float friction, acc, maxSpeed;

		fall = Mathf.MoveToward(fall, -MAX_FALLING_SPEED, GRAVITY * dt);

		//Floor/Air variables
		Vector3 snap = Vector3.Zero;
		if(IsOnFloor())
		{
			friction = FLOOR_FRICTION;
			acc = isCrouched ? CROUCH_FLOOR_ACC : FULL_FLOOR_ACC;

			if(justJumped)
				snap = Vector3.Zero;
			else
			{
				snap = -GetFloorNormal();
				fall = 0;
			}
		}
		else
		{
			friction = AIR_FRICTION;
			acc = isCrouched ? CROUCH_AIR_ACC : FULL_AIR_ACC;
			snap = Vector3.Down;
		}

		maxSpeed = isCrouched ? CROUCH_MAX_SPEED : FULL_MAX_SPEED;
		debugVars.AddOrSet("globalMaxSpeed", maxSpeed);

		//Horizontal movement
		HorizontalMovement(friction, acc, maxSpeed, dt);
		
		//Moving
		Vector3 finalVelocity = new Vector3(velocity.x, fall, velocity.z);
		Vector3 result = MoveAndSlideWithSnap(finalVelocity, snap, Vector3.Up);

		if(IsOnCeiling()) 
			fall = result.y;

		return result;
	}

	private void HorizontalMovement(float friction, float acc, float maxSpeed, float dt)
	{
		if(IsOnFloor())
			velocity *= friction * 60 * dt;

		// Recreation of MoveToward without able to go away from destination
		// Func<Vector3, Vector3, float, Vector3> customMoveToward = (from, to, delta) => {
		// 	Vector3 deltaDir = from.DirectionTo(to);
		// 	Vector3 deltaVec = deltaDir * Mathf.Clamp(delta, 0, from.DistanceTo(to));
		// 	Vector3 final = from + deltaVec;

		// 	return final;
		// };

		if(movingDir != Vector3.Zero)
		{
			float surpassOffset = Mathf.Max(velocity.Length() - maxSpeed, 0);
			velocity = velocity.MoveToward(movingDir * (maxSpeed + surpassOffset), acc * dt);
		}
	}

	private Vector3 LadderMovement(float dt)
	{
		fall = 0;

		//Remove local z
		Vector3 localMovingDir = Ladder.ToLocal(movingDir + Ladder.Translation);
		localMovingDir.z = 0;
		Vector3 horizontalMovement = Ladder.ToGlobal(localMovingDir) - Ladder.Translation;

		//Calculate velocity
		float verticalInfluence = -Ladder.Transform.basis.Tdotz(movingDir);

		velocity = horizontalMovement * LADDER_MOVEMENT_SPEED;
		velocity.y = LADDER_MOVEMENT_SPEED * verticalInfluence;

		return MoveAndSlide(velocity, Vector3.Up);		
	}

	private Vector3 WaterMovement(float dt)
	{
		throw new Exception("water movement isnt implemented yet");
	}

	//Ladder
    protected Ladder Ladder { get; private set; }
	
    public void OnLadderEnter(Ladder ladder)  {
		moveType = MoveType.Ladder;
		Ladder = ladder;
	}

    public void OnLadderLeft(Ladder ladder)  {
		GetOffLadder();
	} 
		
    protected void GetOffLadder() { 
		moveType = MoveType.Ground;
		Ladder = null;
	}

	public override void _Input(InputEvent ev)
	{
		if(ev is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
			Looking(mouseMotion.Relative);

		if(ev is InputEventMouseButton mouseEv)
			if(mouseEv.IsPressed())
				if(mouseEv.ButtonIndex == (int)ButtonList.WheelDown)
					Jump();
	}

	private void Controls(float dt)
	{
		movingDir = Walking(dt);

		if(justJumped) justJumped = false;

		if(Input.IsActionJustPressed("jump"))
			Jump();

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
			Shoot();

		if(Input.IsActionJustPressed("reload"))
			reloadTimer.Start();

		for(int i = 0; i < 3; i++)
			if(Input.IsActionJustPressed("switch_weapon_" + i))
				SwitchWeapon(i);

		GetNode<Label>("/root/World/Control/reloadTimer").Text = reloadTimer.TimeLeft.ToString();
	}

	private void Shoot()
	{
		bool shot = currentWeapon.Shoot(camera.GlobalTranslation, -camera.Transform.basis.z, map);

		if(shot)
		{
			var weaponModel = weaponOrigin.GetChild<Spatial>(0);
			var animPlayer = weaponModel.GetNode<AnimationPlayer>("AnimationPlayer");
			animPlayer.Stop();
			animPlayer.Play("shoot");
		}
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
		if(moveType == MoveType.Ladder)
		{
			justJumped = true;

			Vector3 ladderBackward = Ladder.Transform.basis.z;
			velocity = ladderBackward * ladderJumpOffForce;
			GetOffLadder();
		}
		else
		{
			if(IsOnFloor())
			{
				justJumped = true;
				fall = JUMP_FORCE;
			}
		}
	}

	private void Looking(Vector2 relative)
	{
		Vector3 newRot = camera.Rotation;
		newRot.y -= relative.x * sensetivity;
		newRot.x -= relative.y * sensetivity;
		newRot.x = Mathf.Clamp(newRot.x, MIN_YAW, MAX_YAW);
		camera.Rotation = newRot;
	}

	private void SetCrouch(bool crouch)
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

	private void Death()
	{
		velocity = Vector3.Zero;
		fall = 0;
		Translation = spawnPos;
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

	public override	void OnHitscanHit()
	{

	}

	public override void OnProjectileHit()
	{
		
	}

	public void _on_full_max_speed_value_changed(float val) => FULL_MAX_SPEED = val;
	public void _on_crouch_max_speed_value_changed(float val) => CROUCH_MAX_SPEED = val;
	public void _on_full_air_acc_value_changed(float val) => FULL_AIR_ACC = val;
	public void _on_crouch_air_acc_value_changed(float val) => CROUCH_AIR_ACC = val;
	public void _on_full_floor_acc_value_changed(float val) => FULL_FLOOR_ACC = val;
	public void _on_crouch_floor_acc_value_changed(float val) => CROUCH_FLOOR_ACC = val;

	public void _on_gravity_value_changed(float val) => GRAVITY = val;
	public void _on_jump_force_value_changed(float val) => JUMP_FORCE = val; 
	public void _on_max_falling_speed_value_changed(float val) => MAX_FALLING_SPEED = val;
	public void _on_sens_value_changed(float val) => sensetivity = val;
	public void _on_air_friction_value_changed(float val) => AIR_FRICTION = val;
	public void _on_floor_friction_value_changed(float val) => FLOOR_FRICTION = val;
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
using Godot;
using System.Collections.Generic;

struct PlayerInfo {
    public bool IsOnFloor;
    public bool IsOnCeiling;
    public bool IsOnWall;

    public Vector3 MovingDir;
    public Vector3 FloorNormal;
    public bool IsCrouched;
    public bool JustJumped;
    public float FloorMaxAngle;
}

class PlayerMovement
{
    //Constants
    float GRAVITY = 70f;
    float MAX_FALLING_SPEED = 100f;
    float JUMP_FORCE = 22f;
    
    float AIR_FRICTION = 0.99f;
    float FLOOR_FRICTION = 0.85f;
    float FULL_MAX_SPEED = 25f;
    float CROUCH_MAX_SPEED = 10f;

    const float ACC_PERCENT_RAISE = 130f;
    float FULL_FLOOR_ACC 		= 130f + (130f/100f * ACC_PERCENT_RAISE);
    float FULL_AIR_ACC 			= 40f + (40f/100f * ACC_PERCENT_RAISE);
    float CROUCH_FLOOR_ACC 		= 30f + (30f/100f * ACC_PERCENT_RAISE);
    float CROUCH_AIR_ACC 		= 20f + (20f/100f * ACC_PERCENT_RAISE);

    Dictionary<string, object> debugVars;
    Vector3 velocity;
	float fall;

    Vector3 snap;
    public Vector3 Snap => snap; 

    public PlayerMovement(Dictionary<string, object> debugVars)
    {
        this.debugVars = debugVars;
    }

    public Vector3 Process(float dt, PlayerInfo info)
    {
        return GroundMovement(dt, info);
    }

    public void PostProcess(float dt, PlayerInfo info, Vector3 movingResult, KinematicCollision[] collisions)
    {
        PostGroundMovement(dt, info, movingResult, collisions);
    }

    public void ApplyJumpForce()
    {
        fall = JUMP_FORCE;
    }

    Vector3 GroundMovement(float dt, PlayerInfo info)
	{
		float friction, acc, maxSpeed;

		fall = Mathf.MoveToward(fall, -MAX_FALLING_SPEED, GRAVITY * dt);

		//Floor/Air variables
		if(info.IsOnFloor)
		{
			friction = FLOOR_FRICTION;
			acc = info.IsCrouched ? CROUCH_FLOOR_ACC : FULL_FLOOR_ACC;

			if(info.JustJumped)
				snap = Vector3.Zero;
			else
			{
				snap = -info.FloorNormal;
				fall = 0;
			}
		}
		else
		{
			friction = AIR_FRICTION;
			acc = info.IsCrouched ? CROUCH_AIR_ACC : FULL_AIR_ACC;
			snap = Vector3.Down;
		}

		maxSpeed = info.IsCrouched ? CROUCH_MAX_SPEED : FULL_MAX_SPEED;
		debugVars.AddOrSet("globalMaxSpeed", maxSpeed);

		//Horizontal movement
		GroundHorizontalMovement(info.MovingDir, friction, acc, maxSpeed, dt);

		debugVars.AddOrSet("fall", fall);

        return new Vector3(velocity.x, fall, velocity.z);
	}

    void PostGroundMovement(float dt, PlayerInfo info, Vector3 movingResult, KinematicCollision[] collisions)
    {
        if(info.IsOnCeiling)
			fall = movingResult.y;

		if(info.IsOnWall)
		{
			//Finding a wall
			KinematicCollision wall = null;

			foreach(KinematicCollision collision in collisions) {
				Vector3 normal = collision.Normal;
				Vector2 normal2 = new Vector2(normal.z, normal.y);
				normal2.Angle();

				float yAngle = normal.y * Mathf.Deg2Rad(90);

				if(yAngle < info.FloorMaxAngle)
					wall = collision;
			}

			if(wall != null)
			{
				//TODO: make velocity stop on wall collision
			}
		}
    }

    void GroundHorizontalMovement(Vector3 movingDir, float friction, float acc, float maxSpeed, float dt)
	{
		velocity *= friction * 60 * dt;

		// Recreation of MoveToward without able to go away from destination
		// Func<Vector3, Vector3, float, Vector3> customMoveToward = (from, to, delta) => {
		// 	Vector3 deltaDir = from.DirectionTo(to);
		// 	Vector3 deltaVec = deltaDir * Mathf.Clamp(delta, 0, from.DistanceTo(to));
		// 	Vector3 final = from + deltaVec;

		// 	return final;
		// };

		// Recreation of MoveToward that just returns deltaVec
		// Func<Vector3, Vector3, float, Vector3> MoveTowardDelta = (from, to, delta) => {
		// 	Vector3 deltaDir = from.DirectionTo(to);
		// 	Vector3 deltaVec = deltaDir * Mathf.Clamp(delta, 0, from.DistanceTo(to));
		// 	return deltaVec;
		// };

		if(movingDir != Vector3.Zero)
		{
			//It works but its weird
			//float surpassOffset = Mathf.Max(velocity.Length() - maxSpeed, 0);
			//velocity = velocity.MoveToward(movingDir * maxSpeed, acc * dt);

			float currentSpeed = velocity.Length();
			float addAcc = Mathf.Clamp(maxSpeed - currentSpeed, 0, acc * dt);
			//Vector3 deltaDir = velocity.DirectionTo(movingDir);
			//Vector3 deltaVec = addAcc * deltaDir;
			velocity += movingDir * addAcc;

			debugVars.AddOrSet("acc", acc);
			debugVars.AddOrSet("addAcc", addAcc);
			debugVars.AddOrSet("currentSpeed", velocity.Length());
		}
	}
}
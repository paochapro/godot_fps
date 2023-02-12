using Godot;

static class CollisionObjectExtensions
{
    public static void PlaceBulletHoleDecal(this CollisionObject body, Vector3 translation, Vector3 normal, Vector3 dir, Texture texture)
    {
        Sprite3D sprite = new Sprite3D();
        sprite.Texture = texture;
        sprite.Shaded = true;

        body.AddChild(sprite);

        Vector3 z = normal;
        Vector3 y = z.Cross(dir).Normalized();
        Vector3 x = y.Cross(z).Normalized();

        sprite.GlobalTransform = new Transform(new Basis(x,y,z), translation);
    }
}
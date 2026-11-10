using DogSimulator.Networking;
using DogSimulator.Player;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace DogSimulator.Editor
{
    public static partial class EditorFirstMigration
    {
        private static void BuildCourse(SimplePolyCityWorld world)
        {
            var root = new GameObject("Golden Bone Courtyard").transform;
            root.SetParent(world.transform, false);
            var course = root.gameObject.AddComponent<GoldenBoneCourse>();
            var navy = MaterialAsset("Course_Navy", new Color(.12f, .28f, .44f));
            var blue = MaterialAsset("Course_Blue", new Color(.13f, .59f, .83f));
            var gold = MaterialAsset("Course_Gold", new Color(.98f, .65f, .16f));
            var dark = MaterialAsset("Course_Dark", new Color(.34f, .22f, .12f));
            var plateRest = MaterialAsset("Course_PlateRest", new Color(.74f, .25f, .16f));
            var platePressed = MaterialAsset("Course_PlatePressed", new Color(.33f, .78f, .34f));

            Block("Golden Bone Pedestal", GoldenBoneCourse.Pedestal - Vector3.up * .57f, new Vector3(1.35f, .82f, 1.35f), gold);
            Block("West Low Fence", GoldenBoneCourse.Vault + new Vector3(-4.5f, .62f, 1.7f), new Vector3(.24f, 1.24f, 4.6f), dark);
            Block("South Fence", GoldenBoneCourse.Vault + new Vector3(.2f, .62f, -3.8f), new Vector3(8.9f, 1.24f, .24f), dark);
            Block("East Fence", GoldenBoneCourse.Vault + new Vector3(4.5f, .62f, 0f), new Vector3(.24f, 1.24f, 7.6f), dark);
            Block("North Fence", GoldenBoneCourse.Vault + new Vector3(-2.5f, .62f, 4f), new Vector3(4.2f, 1.24f, .24f), dark);
            var gate = Block("Courtyard Gate", GoldenBoneCourse.GateClosed, GoldenBoneCourse.GateSize, gold);
            var plate = Block("Pressure Plate", GoldenBoneCourse.Switch, new Vector3(1.4f, .2f, 1.45f), plateRest, false);
            var pad = Block("Bounce Pad", GoldenBoneCourse.PadCenter,
                new Vector3(1.8f, .18f, 2.2f), blue);
            pad.gameObject.AddComponent<BouncePad>().CushionLanding = true;

            var kennel = GoldenBoneCourse.Delivery.center;
            Block("Delivery Mat", new Vector3(kennel.x, .25f, kennel.z), new Vector3(2.8f, .08f, 2.8f), blue, false);
            Block("Kennel Roof", new Vector3(kennel.x, 2.35f, kennel.z), new Vector3(3.1f, .25f, 3.1f), blue);
            Block("Kennel Back", new Vector3(kennel.x, 1.26f, kennel.z + 1.38f),
                new Vector3(2.85f, 2.1f, .24f), navy);
            foreach (var side in new[] { -1, 1 })
                Block("Kennel Side", new Vector3(kennel.x + side * 1.38f, 1.26f, kennel.z),
                    new Vector3(.24f, 2.1f, 2.85f), navy);
            Sign("GOLDEN BONE", GoldenBoneCourse.GateClosed + Vector3.up * 1.75f, Quaternion.Euler(0, 90, 0));
            Sign("DELIVERY", new Vector3(kennel.x, 2.75f, kennel.z - 1.5f), Quaternion.identity);

            Set(course, "gate", gate);
            Set(course, "gateCollider", gate.GetComponent<Collider>());
            Set(course, "gateObstacle", gate.GetComponent<NavMeshObstacle>());
            Set(course, "plateRenderer", plate.GetComponent<Renderer>());
            Set(course, "plateRest", plateRest);
            Set(course, "platePressed", platePressed);
            Set(world, "course", course);

            Transform Block(string label, Vector3 position, Vector3 size, Material material, bool solid = true)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = label;
                block.transform.SetParent(root, false);
                block.transform.position = position;
                block.transform.localScale = size;
                block.GetComponent<Renderer>().sharedMaterial = material;
                block.GetComponent<Collider>().enabled = solid;
                if (label.EndsWith("Fence") || label == "Courtyard Gate" || label.StartsWith("Kennel"))
                {
                    var obstacle = block.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.size = Vector3.one;
                    obstacle.carving = true;
                }
                return block.transform;
            }

            void Sign(string label, Vector3 position, Quaternion rotation)
            {
                var sign = new GameObject(label).AddComponent<TextMeshPro>();
                sign.transform.SetParent(root, false);
                sign.transform.SetPositionAndRotation(position, rotation);
                sign.rectTransform.sizeDelta = new Vector2(3.6f, .55f);
                sign.fontSize = 4;
                sign.alignment = TextAlignmentOptions.Center;
                sign.color = Color.white;
                sign.text = label;
            }
        }

        private static void BuildJail(SimplePolyCityWorld world)
        {
            var root = new GameObject("Dog Jail");
            root.transform.SetParent(world.transform, false);
            var jail = root.AddComponent<DogJail>();
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = Vector3.up * (DogJail.Height / 2);
            obstacle.size = new Vector3(10f, DogJail.Height, 8f);
            obstacle.carving = true;
            var metal = MaterialAsset("Jail_Metal", new Color(.18f, .25f, .34f));
            var yellow = MaterialAsset("Jail_Yellow", new Color(1f, .65f, .12f));

            Box(root, "Floor", new Vector3(0, -.14f, 0), new Vector3(10.2f, .28f, 8.2f), metal);
            Box(root, "Roof", new Vector3(0, DogJail.Height + .1f, 0), new Vector3(10.2f, .2f, 8.2f), metal);
            Wall(root, new Vector3(0, 0, -DogJail.HalfDepth), Vector3.right, 10, metal);
            Wall(root, new Vector3(-DogJail.HalfWidth, 0, 0), Vector3.forward, 8, metal);
            Wall(root, new Vector3(DogJail.HalfWidth, 0, 0), Vector3.forward, 8, metal);
            var side = DogJail.HalfWidth - DogJail.GateHalfWidth;
            Wall(root, new Vector3(-(DogJail.HalfWidth + DogJail.GateHalfWidth) / 2, 0, DogJail.HalfDepth), Vector3.right, side, metal);
            Wall(root, new Vector3((DogJail.HalfWidth + DogJail.GateHalfWidth) / 2, 0, DogJail.HalfDepth), Vector3.right, side, metal);
            var gate = new GameObject("Sliding Gate");
            gate.transform.SetParent(root.transform, false);
            Wall(gate, Vector3.forward * DogJail.HalfDepth, Vector3.right, DogJail.GateHalfWidth * 2, yellow);
            Box(root, "Bowl Plate", new Vector3(3, .12f, -1.5f), new Vector3(1.8f, .15f, 1.8f), yellow);
            for (var i = 0; i < 4; i++)
                Box(root, "Plate Trail", new Vector3(-1.45f + 1.05f * i, .012f, -1.6f + .22f * (i % 2)),
                    new Vector3(.19f, .02f, .32f), yellow, false);
            var lever = Box(root, "Rescue Lever", new Vector3(2.35f, .48f, 4.65f),
                new Vector3(.42f, .95f, .42f), yellow).GetComponent<BoxCollider>();
            Box(root, "Dog Bed", new Vector3(2.9f, .11f, -3.1f), new Vector3(2f, .22f, .8f), yellow);
            var sign = new GameObject("Good Dogs Only");
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = new Vector3(0, 2.75f, 4.24f);
            sign.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var text = sign.AddComponent<TextMeshPro>();
            text.text = "GOOD DOGS ONLY";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 3.3f;
            text.color = new Color(1f, .85f, .35f);
            text.rectTransform.sizeDelta = new Vector2(6f, .6f);
            Set(jail, "gate", gate);
            Set(jail, "lever", lever);
            Set(world, "jail", jail);
            root.SetActive(false);
        }

        private static void Wall(GameObject parent, Vector3 center, Vector3 axis, float width, Material material)
        {
            var count = Mathf.CeilToInt(width / .32f);
            for (var i = 0; i <= count; i++)
                Box(parent, "Bar", center + axis * Mathf.Lerp(-width / 2, width / 2, (float)i / count)
                    + Vector3.up * (DogJail.Height / 2), new Vector3(.1f, DogJail.Height, .1f), material, false);
            var solid = parent.AddComponent<BoxCollider>();
            solid.center = center + Vector3.up * (DogJail.Height / 2);
            solid.size = axis == Vector3.right ? new Vector3(width, DogJail.Height, .14f) : new Vector3(.14f, DogJail.Height, width);
        }

        private static GameObject Box(GameObject parent, string name, Vector3 center, Vector3 size, Material material,
            bool collides = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!collides) box.GetComponent<Collider>().enabled = false;
            return box;
        }


        private static Material MaterialAsset(string name, Color color)
        {
            var path = $"Assets/Game/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}

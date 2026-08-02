using UnityEngine;

// Процедурная 3D-заглушка врага из примитивов Unity — временная, пока нет настоящего арта/модели.
// Вешается на пустой GameObject в нужном месте боевой сцены; сама строит себя из капсулы (тело),
// сферы (голова) и двух капсул (руки). [ExecuteAlways] — пересобирается сразу в Editor, без Play Mode.
[ExecuteAlways]
public class EnemyPlaceholderModel : MonoBehaviour
{
    [Header("Пропорции")]
    public float bodyHeight = 1.4f;
    public float bodyRadius = 0.35f;
    public float headRadius = 0.3f;
    public float armLength = 0.7f;
    public float armRadius = 0.12f;

    [Header("Цвет (единый материал на всю фигуру)")]
    public Color bodyColor = new Color(0.6f, 0.15f, 0.15f); // тёмно-красный — читается как "враг" издалека

    private Transform built;

    private void OnEnable() => Rebuild();

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (built != null) DestroySafe(built.gameObject);

        var root = new GameObject("PlaceholderModel");
        root.transform.SetParent(transform, false);
        built = root.transform;

        var mat = new Material(Shader.Find("Standard")) { color = bodyColor };

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0, bodyHeight / 2f, 0);
        body.transform.localScale = new Vector3(bodyRadius * 2f, bodyHeight / 2f, bodyRadius * 2f);
        ApplyMaterial(body, mat);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0, bodyHeight + headRadius, 0);
        head.transform.localScale = Vector3.one * headRadius * 2f;
        ApplyMaterial(head, mat);

        CreateArm(root.transform, mat, -1);
        CreateArm(root.transform, mat, 1);

        // Визуальная заглушка, не физический объект — убираем коллайдеры, которые CreatePrimitive
        // добавляет по умолчанию (иначе они мешают кликам/рейкастам по остальной 3D-сцене карты боя).
        foreach (var col in root.GetComponentsInChildren<Collider>())
            DestroySafe(col);
    }

    private void CreateArm(Transform root, Material mat, int side)
    {
        var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        arm.name = side < 0 ? "ArmLeft" : "ArmRight";
        arm.transform.SetParent(root, false);
        arm.transform.localPosition = new Vector3(side * (bodyRadius + armRadius), bodyHeight * 0.65f, 0);
        arm.transform.localRotation = Quaternion.Euler(0, 0, side * 80f);
        arm.transform.localScale = new Vector3(armRadius * 2f, armLength / 2f, armRadius * 2f);
        ApplyMaterial(arm, mat);
    }

    private static void ApplyMaterial(GameObject obj, Material mat)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
    }

    private static void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}

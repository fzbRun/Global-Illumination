using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{

	[SerializeField]
	CameraSetting settings = default;

	public CameraSetting Settings => settings ?? (settings = new CameraSetting());
}
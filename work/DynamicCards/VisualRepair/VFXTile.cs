using UnityEngine;

public class VFXTile : MonoBehaviour
{
	public ParticleSystemRenderer[] CurvedEffects;

	public static int SHADER_CAM_POSITION_KEYWORD = -1;

	private MeshRenderer m_MeshRenderer;

	private void OnEnable()
	{
		if (SHADER_CAM_POSITION_KEYWORD == -1)
		{
			SHADER_CAM_POSITION_KEYWORD = Shader.PropertyToID("_CamPosition");
		}
		if (m_MeshRenderer == null)
		{
			m_MeshRenderer = base.gameObject.GetComponent<MeshRenderer>();
		}
	}

	public void SetCameraPosition(Vector4 _WorldSpaceCamPos)
	{
		int num = CurvedEffects.Length;
		for (int i = 0; i < num; i++)
		{
			CurvedEffects[i].sharedMaterial.SetVector(SHADER_CAM_POSITION_KEYWORD, _WorldSpaceCamPos);
		}
		m_MeshRenderer.material.SetVector(SHADER_CAM_POSITION_KEYWORD, _WorldSpaceCamPos);
	}
}

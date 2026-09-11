using System.Collections.Generic;
using UnityEngine;

namespace GwentUnity;

public class VFXTileMotion : MonoBehaviour
{
	public List<GameObject> TilePrefabs = new List<GameObject>();

	private List<GameObject> Pool = new List<GameObject>();

	private List<VFXTile> VFXTiles = new List<VFXTile>();

	public Vector3 StartPoint;

	public Vector3 EndPoint;

	public float Speed = 5f;

	public int TileNum = 6;

	private Vector3 Direction;

	public float TileSpacing;

	public Transform WorldSpaceCamPos;

	private Vector4 Pos;

	private void OnEnable()
	{
		Direction = (EndPoint - StartPoint).normalized;
		int num = 0;
		for (int i = 0; i < TileNum; i++)
		{
			GameObject gameObject = Object.Instantiate(TilePrefabs[num]);
			gameObject.transform.SetParent(base.transform);
			gameObject.transform.localScale = Vector3.one;
			gameObject.transform.localPosition = StartPoint + Direction * TileSpacing * i;
			base.transform.localRotation = Quaternion.identity;
			VFXTiles.Add(gameObject.GetComponent<VFXTile>());
			Pool.Add(gameObject);
			num = ((num < TilePrefabs.Count - 1) ? (num + 1) : 0);
		}
	}

	private void Update()
	{
		Pos = new Vector4(WorldSpaceCamPos.position.x, WorldSpaceCamPos.position.y, WorldSpaceCamPos.position.z, 0f);
		for (int i = 0; i < Pool.Count; i++)
		{
			VFXTiles[i].SetCameraPosition(Pos);
			Pool[i].transform.Translate(Direction * Speed * Time.deltaTime);
			if (Pool[i].transform.localPosition.z >= EndPoint.z)
			{
				Pool[i].transform.localPosition = StartPoint + Direction * Vector3.Distance(Pool[i].transform.localPosition, EndPoint);
			}
		}
	}
}

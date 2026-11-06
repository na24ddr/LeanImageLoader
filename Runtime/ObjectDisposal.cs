using UnityEngine;

namespace LeanImageLoader
{
	/// <summary>
	/// Destroys Unity objects in both Play Mode and Edit Mode (editor tools, Edit Mode tests).
	/// </summary>
	internal static class ObjectDisposal
	{
		/// <summary>
		/// Destroys <paramref name="obj"/> if it is not null.
		/// </summary>
		/// <param name="obj">Object to destroy.</param>
		public static void Destroy(Object obj)
		{
			if (obj == null)
			{
				return;
			}

			if (Application.isPlaying)
			{
				Object.Destroy(obj);
			}
			else
			{
				Object.DestroyImmediate(obj);
			}
		}
	}
}

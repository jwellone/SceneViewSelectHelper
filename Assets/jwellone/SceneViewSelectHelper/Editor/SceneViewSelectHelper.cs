using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

#nullable enable

namespace jwelloneEditor
{
	[InitializeOnLoad]
	public static class SceneViewSelectHelper
	{
		private static readonly Type tSceneViewPicking;
		private static readonly MethodInfo miGetAllOverlapping;


		static SceneViewSelectHelper()
		{
			SceneView.duringSceneGui -= OnDuringSceneGui;
			SceneView.duringSceneGui += OnDuringSceneGui;

			tSceneViewPicking = Type.GetType("UnityEditor.SceneViewPicking,UnityEditor");
			miGetAllOverlapping = tSceneViewPicking.GetMethod("GetAllOverlapping", BindingFlags.NonPublic | BindingFlags.Static);
		}

		static void OnDuringSceneGui(SceneView sceneView)
		{
			var e = Event.current;
			if (e.type == EventType.Used || !e.control || !e.isMouse || e.button != 1)
			{
				return;
			}

			switch (e.rawType)
			{
				case EventType.MouseDown:
					var list = new List<GameObject>();
					foreach (var target in (IEnumerable<GameObject>)miGetAllOverlapping.Invoke(null, new object[] { e.mousePosition }))
					{
						list.Add(target);
					}

					if (list.Count <= 0)
					{
						return;
					}

					list.Sort((a, b) => a.transform.GetSiblingIndex() - b.transform.GetSiblingIndex());
					var rect = new Rect(e.mousePosition.x, e.mousePosition.y, 0, 0);
					PopupWindow.Show(rect, new PopupMenu(list));

					e.Use();
					break;
			}
		}

		class PopupMenu : PopupWindowContent
		{
			Vector2 _scrollPos = Vector2.zero;
			readonly IList<GameObject> _objects;

			public PopupMenu(in IList<GameObject> objects)
			{
				_objects = objects;
			}

			public override Vector2 GetWindowSize()
			{
				var height = Mathf.Min(26 + _objects.Count * 20, 226);
				return new Vector2(160, height);
			}

			public override void OnGUI(Rect rect)
			{
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("X", GUILayout.Width(20)))
				{
					editorWindow.Close();
				}
				GUILayout.EndHorizontal();

				_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);


				foreach (var obj in _objects)
				{
					EditorGUILayout.ObjectField(obj, typeof(GameObject), false);
				}

				EditorGUILayout.EndScrollView();
			}

			public override void OnOpen()
			{
			}

			public override void OnClose()
			{
			}
		}
	}
}
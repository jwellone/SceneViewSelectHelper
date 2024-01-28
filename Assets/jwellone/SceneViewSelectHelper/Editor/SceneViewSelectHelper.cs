using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Linq;

#nullable enable

namespace jwelloneEditor
{
	[InitializeOnLoad]
	public static class SceneViewSelectHelper
	{
		const BindingFlags BIND_FLAGS = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

		static Type? _tSceneViewPicking;
		static Type tSceneViewPicking => _tSceneViewPicking ?? (_tSceneViewPicking = Type.GetType("UnityEditor.SceneViewPicking,UnityEditor"));

#if UNITY_2022_2_OR_NEWER

		static Type? _tPickingClass;
        static Type tPickingClass => _tPickingClass ?? (_tPickingClass = Type.GetType("UnityEditor.PickingObject,UnityEditor"));

        static MethodInfo? _miTryGetGameObjectMethod;
        static MethodInfo? miTryGetGameObjectMethod => _miTryGetGameObjectMethod??(_miTryGetGameObjectMethod= tPickingClass.GetMethod("TryGetGameObject", BindingFlags.Public | BindingFlags.Instance));

#endif
		static MethodInfo? _miGetAllOverlapping;
		static MethodInfo miGetAllOverlapping => _miGetAllOverlapping ?? (_miGetAllOverlapping = tSceneViewPicking.GetMethod("GetAllOverlapping", BIND_FLAGS));

		static Type? _tAnnotation;
		static Type tAnnotation => _tAnnotation ?? (_tAnnotation = Type.GetType("UnityEditor.Annotation, UnityEditor"));

		static Type? _tAnnotationUtility;
		static Type tAnnotationUtility => _tAnnotationUtility ?? (_tAnnotationUtility = Type.GetType("UnityEditor.AnnotationUtility, UnityEditor"));

		static FieldInfo? _fiClassId;
		static FieldInfo fiClassId => _fiClassId ?? (_fiClassId = tAnnotation.GetField("classID"));

		static FieldInfo? _fiScriptClass;
		static FieldInfo fiScriptClass => _fiScriptClass ?? (_fiScriptClass = tAnnotation.GetField("scriptClass"));

		static MethodInfo? _miGetAnnotations;
		static MethodInfo miGetAnnotations => _miGetAnnotations ?? (_miGetAnnotations = tAnnotationUtility.GetMethod("GetAnnotations", BIND_FLAGS));

		static MethodInfo? _miSetGizmoEnabled;
		static MethodInfo miSetGizmoEnabled => _miSetGizmoEnabled ?? (_miSetGizmoEnabled = tAnnotationUtility.GetMethod("SetGizmoEnabled", BIND_FLAGS));

		static MethodInfo? _miSetIconEnabled;
		static MethodInfo miSetIconEnabled => _miSetIconEnabled ?? (_miSetIconEnabled = tAnnotationUtility.GetMethod("SetIconEnabled", BIND_FLAGS));

		static SceneViewSelectHelper()
		{
			SceneView.duringSceneGui -= OnDuringSceneGui;
			SceneView.duringSceneGui += OnDuringSceneGui;
		}

		static void OnDuringSceneGui(SceneView sceneView)
		{
			var e = Event.current;
			if (e.type == EventType.Used || !e.control || !e.isMouse || e.button != 1 || e.rawType != EventType.MouseDown)
			{
				return;
			}

			var list = new List<GameObject>();

#if UNITY_2022_2_OR_NEWER
			if (SceneViewSelectHelper.tPickingClass != null)
			{
				// 调用 miGetAllOverlapping.Invoke 获取结果
				var result = miGetAllOverlapping.Invoke(null, new object[] { e.mousePosition });

				if (result is IEnumerable enumerableResult)
				{

					foreach (object obj in enumerableResult)
					{
				
						
							object[] parameters = new object[] { null };
							bool success = (bool)miTryGetGameObjectMethod.Invoke(obj, parameters);

							if (success)
							{
								GameObject gameObject = (GameObject)parameters[0];
								list.Add(gameObject);
							}
						
					}

				}
			}
#else
		foreach (var target in (IEnumerable<GameObject>)miGetAllOverlapping.Invoke(null, new object[] { e.mousePosition }))
			{
				list.Add(target);
			}
#endif
            if (list.Count <= 0)
			{
				return;
			}

			list.Sort((a, b) => a.transform.GetSiblingIndex() - b.transform.GetSiblingIndex());
			var rect = new Rect(e.mousePosition.x, e.mousePosition.y, 0, 0);
			PopupWindow.Show(rect, new PopupMenu(list));

			e.Use();
		}

		public static void SetIconEnabled(bool enabled)
		{
			var annotations = (Array)miGetAnnotations.Invoke(null, null);
			var param = new object[] { 0, "", 0 };
			foreach (var a in annotations)
			{
				param[0] = (int)fiClassId.GetValue(a);
				param[1] = (string)fiScriptClass.GetValue(a);
				param[2] = Convert.ToInt32(enabled);
				miSetIconEnabled.Invoke(null, param);
			}
		}

		public static void SetGizmoEnabled(bool enabled)
		{
			var annotations = (Array)miGetAnnotations.Invoke(null, null);
			var param = new object[] { 0, "", 0, true };
			foreach (var a in annotations)
			{
				param[0] = (int)fiClassId.GetValue(a);
				param[1] = (string)fiScriptClass.GetValue(a);
				param[2] = Convert.ToInt32(enabled);
				miSetGizmoEnabled.Invoke(null, param);
			}
		}

		class PopupMenu : PopupWindowContent
		{
			class Data
			{
				public readonly GameObject gameObject;
				public readonly Texture2D[] textures;

				public Data(in GameObject gameObject)
				{
					this.gameObject = gameObject;
					var components = gameObject.GetComponents<Component>();
					textures = new Texture2D[components.Length];
					for (var i = 0; i < components.Length; ++i)
					{
						textures[i] = AssetPreview.GetMiniThumbnail(components[i]);
					}
				}
			}

			const int TOGGLE_WIDTH = 12;
			const int FIELD_WIDTH = 188;
			const int ICON_WIDTH = 16;

			readonly int _windowWidth;
			readonly Data[] _data;
			Vector2 _scrollPos = Vector2.zero;

			public PopupMenu(in IList<GameObject> objects)
			{
				var iconMaxCount = 0;
				_data = new Data[objects.Count];
				for (var i = 0; i < objects.Count; ++i)
				{
					var data = new Data(objects[i]);
					if (data.textures.Length > iconMaxCount)
					{
						iconMaxCount = data.textures.Length;
					}

					_data[i] = data;
				}

				_windowWidth = TOGGLE_WIDTH + FIELD_WIDTH + (ICON_WIDTH >> 1) + ICON_WIDTH * iconMaxCount;
			}

			public override Vector2 GetWindowSize()
			{
				var width = _windowWidth;
				var height = 26 + 44 + _data.Length * 20;
				if (height >= 306)
				{
					height = 306;
					width += 16;
				}
				return new Vector2(width, height);
			}

			public override void OnGUI(Rect rect)
			{
				var e = Event.current;
				switch (e.rawType)
				{
					case EventType.MouseDrag:
						var position = editorWindow.position;
						position.x += e.delta.x;
						position.y += e.delta.y;
						editorWindow.position = position;
						break;
				}

				if (GUILayout.Button("X", GUILayout.Width(20)))
				{
					editorWindow.Close();
				}

				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Icon");
				if (GUILayout.Button("Show", GUILayout.Width(48)))
				{
					SceneViewSelectHelper.SetIconEnabled(true);
				}
				if (GUILayout.Button("Hide", GUILayout.Width(48)))
				{
					SceneViewSelectHelper.SetIconEnabled(false);
				}
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.BeginHorizontal();
				GUILayout.Label("Gizmo");
				if (GUILayout.Button("Show", GUILayout.Width(48)))
				{
					SceneViewSelectHelper.SetGizmoEnabled(true);
				}
				if (GUILayout.Button("Hide", GUILayout.Width(48)))
				{
					SceneViewSelectHelper.SetGizmoEnabled(false);
				}
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.EndHorizontal();

				_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

				var x = TOGGLE_WIDTH + FIELD_WIDTH - 10;
				foreach (var data in _data)
				{
					EditorGUILayout.BeginHorizontal();
					var active = EditorGUILayout.Toggle(data.gameObject.activeSelf, GUILayout.Width(TOGGLE_WIDTH));
					data.gameObject.SetActive(active);
					EditorGUILayout.ObjectField(data.gameObject, typeof(GameObject), false, GUILayout.Width(FIELD_WIDTH));
					EditorGUILayout.EndHorizontal();

					var lastRect = GUILayoutUtility.GetLastRect();
					lastRect.x = x;
					lastRect.y += 2;
					lastRect.width = ICON_WIDTH;
					lastRect.height = ICON_WIDTH;
					foreach (var tex in data.textures)
					{
						lastRect.x += lastRect.width;
						GUI.DrawTexture(lastRect, tex);
					}
				}

				EditorGUILayout.EndScrollView();
			}

			public override void OnOpen()
			{
				editorWindow.wantsMouseMove = true;
			}

			public override void OnClose()
			{
				editorWindow.wantsMouseMove = false;
			}
		}
	}
}
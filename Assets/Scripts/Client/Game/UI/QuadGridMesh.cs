// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using System.Collections.Generic;
using Imma.Common;
using Imma.Foobbuteo.Common.dev;
using Imma.Foobbuteo.Shared.Game;
using TMPro;
using UnityEngine;

namespace Imma.Foobbuteo.Client.Game.UI
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class QuadGridMesh : MonoBehaviour
    {
        [Header("Grid Dimensions")]
        [ReadOnly]
        public Vector2 totalSize = new(
            CoordinateConverter.Right - CoordinateConverter.Left,
            CoordinateConverter.Top - CoordinateConverter.Bottom
        );
        private float CellWidth => totalSize.x / Mathf.Max(1f, columns);
        private float CellHeight => totalSize.y / Mathf.Max(1f, rows);

        public int columns = 5;
        public int rows = 5;
        public float lineThickness = 0.02f;

        public TMP_Text textPrefab;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;

        private void Awake()
        {
            _meshFilter ??= GetComponent<MeshFilter>();
            _meshRenderer ??= GetComponent<MeshRenderer>();
        }

        private void Start()
        {
            GenerateGrid();
            GenerateCellLabels();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                _meshFilter ??= GetComponent<MeshFilter>();
                _meshRenderer ??= GetComponent<MeshRenderer>();
                GenerateGrid();
            }
        }

        private void GenerateGrid()
        {
            InitMesh();

            // List<Vector3> vertices = new();
            // List<int> triangles = new();
            // List<Vector2> uvs = new();

            int lineCount = (columns - 1) + (rows - 1);

            List<Vector3> vertices = new(lineCount * 4);
            List<int> triangles = new(lineCount * 6);
            List<Vector2> uvs = new(lineCount * 4);

            // Vertical lines
            for (int x = 1; x < columns; x++)
            {
                Vector3 start = new(CoordinateConverter.Left + x * CellWidth, CoordinateConverter.Top, 0);
                Vector3 end = new(
                    CoordinateConverter.Left + x * CellWidth,
                    CoordinateConverter.Top + rows * CellHeight,
                    0
                );

                AddQuadLine(start, end);
            }

            // Horizontal lines
            for (int y = 1; y < rows; y++)
            {
                Vector3 start = new(CoordinateConverter.Left, CoordinateConverter.Top + y * CellHeight, 0f);
                Vector3 end = new(
                    CoordinateConverter.Left + columns * CellWidth,
                    CoordinateConverter.Top + y * CellHeight,
                    0f
                );
                AddQuadLine(start, end);
            }

            GenerateMesh(vertices, triangles, uvs);

            // =========================
            // Local function
            // =========================

            void AddQuadLine(Vector3 start, Vector3 end)
            {
                int index = vertices.Count;

                Vector3 direction = (end - start).normalized;

                Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0) * (lineThickness * 0.5f);

                Vector3 v0 = start - perpendicular;
                Vector3 v1 = start + perpendicular;
                Vector3 v2 = end + perpendicular;
                Vector3 v3 = end - perpendicular;

                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);

                float length = Vector3.Distance(start, end);

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(length, 1));
                uvs.Add(new Vector2(length, 0));

                triangles.Add(index + 0);
                triangles.Add(index + 1);
                triangles.Add(index + 2);

                triangles.Add(index + 0);
                triangles.Add(index + 2);
                triangles.Add(index + 3);
            }
        }

        private void GenerateMesh(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
        {
            _mesh.SetVertices(vertices);
            _mesh.SetTriangles(triangles, 0);
            _mesh.SetUVs(0, uvs);

            // mesh.RecalculateNormals();
            Vector3[] normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.forward;
            }

            _mesh.normals = normals;
            // _meshFilter.mesh = _mesh;
            _meshFilter.sharedMesh = _mesh;
            _meshRenderer.sortingLayerName = "Default";
            _meshRenderer.sortingOrder = 5;
        }

        private void InitMesh()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "QuadGrid" };
            }
            else
            {
                _mesh.Clear();
            }
        }

        private void GenerateCellLabels()
        {
            // this.LDebug("p1: " + CoordinateConverter.WorldToLogic(new Vector2(-2.726f, 0.679f)));
            // this.LDebug("p1: " + CoordinateConverter.WorldToLogic(new Vector2(2.726f, -0.679f)));
            if (textPrefab == null)
            {
                this.LogWarn("textPrefab not set");
                return;
            }

            int index = 1;

            int halfColumn = columns / 2;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Vector3 center = new(
                        CoordinateConverter.Left + (x + 0.5f) * CellWidth + (x == halfColumn ? 0.15f : 0.0f),
                        CoordinateConverter.Top + (y + 0.5f) * CellHeight,
                        0
                    );

                    TMP_Text label = Instantiate(textPrefab, transform);
                    label.transform.SetLocalPositionAndRotation(center, Quaternion.identity);
                    label.text = $"{index++}";
                }
            }
        }
    }
}

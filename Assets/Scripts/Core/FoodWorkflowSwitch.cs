using System;
using UnityEngine;

namespace Core
{
    /// <summary>다음 생성에 사용할 데이터만 선택한다. 진행 중 요청은 이미 선택한 워크플로를 유지한다.</summary>
    public sealed class FoodWorkflowSwitch : MonoBehaviour
    {
        [SerializeField, Tooltip("False: existing Detail v2. True: prepared Thai food test workflow. Applies to the next generation.")]
        private bool isTest;
        [SerializeField] private TextAsset testWorkflow;

        public bool IsTest { get => isTest; set => isTest = value; }

        public TextAsset SelectDefinition(TextAsset standardWorkflow)
        {
            var selected = isTest ? testWorkflow : standardWorkflow;
            if (selected == null)
                throw new InvalidOperationException(isTest ? "Test workflow definition is missing." : "Standard workflow definition is missing.");
            return selected;
        }
    }
}

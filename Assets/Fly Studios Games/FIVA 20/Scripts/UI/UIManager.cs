using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class UIManager : MonoBehaviour
{
   [Header("Buttons")]
   [FormerlySerializedAs("find_OponentButton")]
   [SerializeField]
   Button findOpponentButton;

   [FormerlySerializedAs("cancel_findOponentButton")]
   [SerializeField]
   Button cancelFindOpponentButton;

   [Header("Managers")]
   [FormerlySerializedAs("findOponentManager")]
   [SerializeField]
   FindOponentManager findOpponentManager;

   void Awake()
   {
      BindListeners();
   }

   void OnDestroy()
   {
      UnbindListeners();
   }

   void BindListeners()
   {
      if (findOpponentButton != null)
      {
         findOpponentButton.onClick.RemoveListener(OnFindOpponentClicked);
         findOpponentButton.onClick.AddListener(OnFindOpponentClicked);
      }

      if (cancelFindOpponentButton != null)
      {
         cancelFindOpponentButton.onClick.RemoveListener(OnCancelFindOpponentClicked);
         cancelFindOpponentButton.onClick.AddListener(OnCancelFindOpponentClicked);
      }
   }

   void UnbindListeners()
   {
      if (findOpponentButton != null)
         findOpponentButton.onClick.RemoveListener(OnFindOpponentClicked);

      if (cancelFindOpponentButton != null)
         cancelFindOpponentButton.onClick.RemoveListener(OnCancelFindOpponentClicked);
   }

   void OnFindOpponentClicked()
   {
      if (findOpponentManager != null)
         findOpponentManager.BeginFindOpponent();
   }

   void OnCancelFindOpponentClicked()
   {
      if (findOpponentManager != null)
         findOpponentManager.CancelFindOpponent();
   }
}

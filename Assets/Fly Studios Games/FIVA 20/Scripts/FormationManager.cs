using System.Collections;
using System.Collections.Generic;
using Assets.SoccerGameEngine_Basic_.Scripts.Utilities.Enums;
using UnityEngine;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Entities
{
    [System.Serializable]
    public class TeamKitMaterials
    {
        public Material body;
        public Material boots;
        public Material eyes;
        public Material gloaves;
        public Material head;
        public Material kitbody;
        public Material socks;
    }

    public class FormationManager : MonoBehaviour
    {
        [Header("team 1 formation")]
        public Team team_1;
        public Color formation_team_1_Color = Color.cyan;
        public TeamKitMaterials team_1_materials = new TeamKitMaterials();
        public Color formation_team_1_goalkeeper_Color = Color.yellow;
        public TeamKitMaterials team_1_goalkeeper_materials = new TeamKitMaterials();

        [Header("team 2 formation")]
        public Team team_2;
        public Color formation_team_2_Color = Color.red;
        public TeamKitMaterials team_2_materials = new TeamKitMaterials();
        public Color formation_team_2_goalkeeper_Color = new Color(1f, 0.5f, 0f, 1f);
        public TeamKitMaterials team_2_goalkeeper_materials = new TeamKitMaterials();

        [Header("Apply")]
        [SerializeField]
        bool applyOnStart = true;

        [SerializeField]
        [Range(1, 300)]
        int applyRetryFrames = 90;

        void Start()
        {
            if (!applyOnStart)
                return;

            StartCoroutine(ApplyWhenTeamsReady());
        }

        IEnumerator ApplyWhenTeamsReady()
        {
            int framesLeft = Mathf.Max(1, applyRetryFrames);
            while (framesLeft-- > 0)
            {
                bool hasAnyPlayer = HasAnyPlayers(team_1) || HasAnyPlayers(team_2);
                if (hasAnyPlayer)
                    break;

                yield return null;
            }

            ApplyFormationDataToPlayers();
        }

        public void ApplyFormationDataToPlayers()
        {
            ApplyTeamData(
                team_1,
                formation_team_1_Color,
                team_1_materials,
                formation_team_1_goalkeeper_Color,
                team_1_goalkeeper_materials);

            ApplyTeamData(
                team_2,
                formation_team_2_Color,
                team_2_materials,
                formation_team_2_goalkeeper_Color,
                team_2_goalkeeper_materials);
        }

        void ApplyTeamData(
            Team team,
            Color teamColor,
            TeamKitMaterials materials,
            Color goalkeeperColor,
            TeamKitMaterials goalkeeperMaterials)
        {
            if (team == null || team.Players == null)
                return;

            for (int i = 0; i < team.Players.Count; i++)
            {
                TeamPlayer teamPlayer = team.Players[i];
                if (teamPlayer == null || teamPlayer.Player == null)
                    continue;

                bool isGoalkeeper = teamPlayer.Player.PlayerType == PlayerTypes.Goalkeeper;
                TeamKitMaterials selectedMaterials = isGoalkeeper && HasAnyMaterial(goalkeeperMaterials)
                    ? goalkeeperMaterials
                    : materials;
                Color selectedColor = isGoalkeeper
                    ? goalkeeperColor
                    : teamColor;

                teamPlayer.Player.ApplyFormationWidget(selectedColor);
                teamPlayer.Player.ApplyTeamMaterials(
                    selectedMaterials != null ? selectedMaterials.body : null,
                    selectedMaterials != null ? selectedMaterials.boots : null,
                    selectedMaterials != null ? selectedMaterials.eyes : null,
                    selectedMaterials != null ? selectedMaterials.gloaves : null,
                    selectedMaterials != null ? selectedMaterials.head : null,
                    selectedMaterials != null ? selectedMaterials.kitbody : null,
                    selectedMaterials != null ? selectedMaterials.socks : null);
            }
        }

        bool HasAnyMaterial(TeamKitMaterials materials)
        {
            return materials != null
                && (materials.body != null
                || materials.boots != null
                || materials.eyes != null
                || materials.gloaves != null
                || materials.head != null
                || materials.kitbody != null
                || materials.socks != null);
        }

        bool HasAnyPlayers(Team team)
        {
            return team != null
                && team.Players != null
                && team.Players.Count > 0;
        }

        void OnValidate()
        {
            applyRetryFrames = Mathf.Clamp(applyRetryFrames, 1, 300);
        }
    }
}

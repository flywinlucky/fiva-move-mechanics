using System.Collections;
using System.Collections.Generic;
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

        [Header("team 2 formation")]
        public Team team_2;
        public Color formation_team_2_Color = Color.red;
        public TeamKitMaterials team_2_materials = new TeamKitMaterials();

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
            ApplyTeamData(team_1, formation_team_1_Color, team_1_materials);
            ApplyTeamData(team_2, formation_team_2_Color, team_2_materials);
        }

        void ApplyTeamData(Team team, Color teamColor, TeamKitMaterials materials)
        {
            if (team == null || team.Players == null)
                return;

            for (int i = 0; i < team.Players.Count; i++)
            {
                TeamPlayer teamPlayer = team.Players[i];
                if (teamPlayer == null || teamPlayer.Player == null)
                    continue;

                teamPlayer.Player.ApplyFormationWidget(teamColor);
                teamPlayer.Player.ApplyTeamMaterials(
                    materials != null ? materials.body : null,
                    materials != null ? materials.boots : null,
                    materials != null ? materials.eyes : null,
                    materials != null ? materials.gloaves : null,
                    materials != null ? materials.head : null,
                    materials != null ? materials.kitbody : null,
                    materials != null ? materials.socks : null);
            }
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

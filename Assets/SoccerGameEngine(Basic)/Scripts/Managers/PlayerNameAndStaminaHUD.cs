using Assets.SoccerGameEngine_Basic_.Scripts.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.SoccerGameEngine_Basic_.Scripts.Managers
{
    public class PlayerNameAndStaminaHUD : MonoBehaviour
    {
        [Header("User Corner")]
        public TMP_Text UserNameText;
        public Slider UserStaminaSlider;

        [Header("Opponent Corner")]
        public TMP_Text OpponentNameText;
        public Slider OpponentStaminaSlider;

        [Header("UI Text")]
        public string EmptyNameText = "-";

        [Header("Refresh")]
        [Range(0.02f, 0.5f)]
        public float RefreshInterval = 0.05f;

        float _nextRefreshTime;

        void Awake()
        {
            ConfigureSlider(UserStaminaSlider);
            ConfigureSlider(OpponentStaminaSlider);
        }

        void Update()
        {
            if (Time.time < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.time + RefreshInterval;
            RefreshHUD();
        }

        void RefreshHUD()
        {
            Team userTeam = GetUserTeam();
            if (userTeam == null)
            {
                SetPlayerUI(null, UserNameText, UserStaminaSlider);
                SetPlayerUI(null, OpponentNameText, OpponentStaminaSlider);
                return;
            }

            Player userPlayer = GetCurrentUserPlayer(userTeam);
            SetPlayerUI(userPlayer, UserNameText, UserStaminaSlider);

            Player closestOpponent = GetClosestOpponentToBall(userTeam);
            SetPlayerUI(closestOpponent, OpponentNameText, OpponentStaminaSlider);
        }

        Team GetUserTeam()
        {
            if (MatchManager.Instance == null)
                return null;

            Team home = MatchManager.Instance.TeamHome;
            if (home != null && home.IsUserControlled)
                return home;

            Team away = MatchManager.Instance.TeamAway;
            if (away != null && away.IsUserControlled)
                return away;

            return null;
        }

        Player GetCurrentUserPlayer(Team userTeam)
        {
            Ball ball = Ball.Instance;
            if (ball != null && ball.Owner != null && ball.Owner.IsUserControlled)
                return ball.Owner;

            if (userTeam.ControllingPlayer != null)
                return userTeam.ControllingPlayer;

            Vector3 refPosition = ball != null ? ball.NormalizedPosition : userTeam.transform.position;
            TeamPlayer closest = userTeam.GetClosestPlayerToPoint(refPosition);
            return closest != null ? closest.Player : null;
        }

        Player GetClosestOpponentToBall(Team userTeam)
        {
            Team opponentTeam = userTeam.Opponent;
            if (opponentTeam == null || opponentTeam.Players == null || opponentTeam.Players.Count == 0)
                return null;

            Ball ball = Ball.Instance;
            Vector3 refPosition = ball != null ? ball.NormalizedPosition : userTeam.transform.position;

            Player closest = null;
            float closestDistance = float.MaxValue;

            foreach (TeamPlayer teamPlayer in opponentTeam.Players)
            {
                if (teamPlayer == null || teamPlayer.Player == null)
                    continue;

                float distance = Vector3.Distance(teamPlayer.Player.Position, refPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = teamPlayer.Player;
                }
            }

            return closest;
        }

        void SetPlayerUI(Player player, TMP_Text nameText, Slider staminaSlider)
        {
            if (nameText != null)
                nameText.text = player != null ? GetDisplayName(player) : EmptyNameText;

            if (staminaSlider == null)
                return;

            if (player == null)
            {
                staminaSlider.minValue = 0f;
                staminaSlider.maxValue = 100f;
                staminaSlider.value = 0f;
                return;
            }

            staminaSlider.minValue = 0f;
            staminaSlider.maxValue = Mathf.Max(1f, player.MaxStamina);
            staminaSlider.value = Mathf.Clamp(player.CurrentStamina, 0f, staminaSlider.maxValue);
        }

        string GetDisplayName(Player player)
        {
            if (!string.IsNullOrWhiteSpace(player.UiPlayerName))
                return player.UiPlayerName;

            return player.name;
        }

        void ConfigureSlider(Slider slider)
        {
            if (slider == null)
                return;

            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 0f;
        }
    }
}

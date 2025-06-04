using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MagicBattle.Common;

namespace MagicBattle.UI
{
    /// <summary>
    /// 데미지 텍스트를 속성별 색상으로 표시하는 UI 클래스
    /// 애니메이션 효과와 함께 데미지를 시각적으로 표현
    /// </summary>
    public class DamageTextUI : MonoBehaviour
    {
        [Header("데미지 텍스트 설정")]
        [SerializeField] private TextMeshProUGUI damageText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("애니메이션 설정")]
        [SerializeField] private float animationDuration = 1.5f;
        [SerializeField] private float moveDistance = 50.0f; // 위로 이동할 거리
        [SerializeField] private Vector3 randomRange = new Vector3(0f, 20f, 0f); // 랜덤 이동 범위

        [Header("속성별 색상")]
        [SerializeField] private Color fireColor = new Color(1f, 0.2f, 0.2f, 1f); // 빨간색
        [SerializeField] private Color iceColor = new Color(0.2f, 0.8f, 1f, 1f); // 하늘색
        [SerializeField] private Color thunderColor = new Color(1f, 1f, 0.2f, 1f); // 전기색 (노란색)
        [SerializeField] private Color basicColor = new Color(1f, 1f, 1f, 1f); // 기본 색상 (흰색)

        [Header("크기 설정")]
        [SerializeField] private float minFontSize = 24f;
        [SerializeField] private float maxFontSize = 48f;
        [SerializeField] private float criticalSizeMultiplier = 1.5f; // 크리티컬 데미지 크기 배수

        // 애니메이션 관련
        private Sequence animationSequence;
        private Vector3 startPosition;
        private bool isAnimating = false;

        // 풀링을 위한 이벤트
        public System.Action<DamageTextUI> OnAnimationComplete;

        private void Awake()
        {
            InitializeComponents();
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            if (damageText == null)
                damageText = GetComponent<TextMeshProUGUI>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// 데미지 텍스트 표시
        /// </summary>
        /// <param name="damage">데미지 값</param>
        /// <param name="attribute">공격 속성</param>
        /// <param name="worldPosition">월드 위치</param>
        /// <param name="isCritical">크리티컬 여부</param>
        public void ShowDamage(float damage, SkillAttribute attribute, Vector3 worldPosition, bool isCritical = false)
        {
            // 진행 중인 애니메이션이 있으면 중지
            if (animationSequence != null && animationSequence.IsActive())
            {
                animationSequence.Kill();
            }

            // 위치 설정
            SetWorldPosition(worldPosition);
            startPosition = transform.position;

            // 텍스트 설정
            SetupDamageText(damage, attribute, isCritical);

            // 애니메이션 시작
            StartAnimation();
        }

        /// <summary>
        /// 월드 좌표를 UI 좌표로 변환하여 설정
        /// </summary>
        /// <param name="worldPosition">월드 위치</param>
        private void SetWorldPosition(Vector3 worldPosition)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                // 약간의 랜덤 오프셋 추가
                Vector3 randomOffset = randomRange;

                Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
                transform.position = screenPosition + randomOffset;
            }
        }

        /// <summary>
        /// 데미지 텍스트 설정
        /// </summary>
        /// <param name="damage">데미지 값</param>
        /// <param name="attribute">공격 속성</param>
        /// <param name="isCritical">크리티컬 여부</param>
        private void SetupDamageText(float damage, SkillAttribute attribute, bool isCritical)
        {
            if (damageText == null) return;

            // 데미지 텍스트 설정
            string damageString = Mathf.RoundToInt(damage).ToString();
            if (isCritical)
            {
                damageString = "CRIT! " + damageString;
            }
            damageText.text = damageString;

            // 속성별 색상 설정
            damageText.color = GetAttributeColor(attribute);

            // 폰트 크기 설정
            float fontSize = Mathf.Lerp(minFontSize, maxFontSize, damage / 100f); // 데미지에 비례
            if (isCritical)
            {
                fontSize *= criticalSizeMultiplier;
            }
            damageText.fontSize = Mathf.Clamp(fontSize, minFontSize, maxFontSize * criticalSizeMultiplier);

            // 초기 투명도 설정
            canvasGroup.alpha = 1f;

            // 초기 스케일 설정 (크리티컬이면 더 크게 시작)
            float initialScale = isCritical ? 1.2f : 0.8f;
            transform.localScale = Vector3.one * initialScale;
        }

        /// <summary>
        /// 속성별 색상 반환
        /// </summary>
        /// <param name="attribute">스킬 속성</param>
        /// <returns>속성에 맞는 색상</returns>
        private Color GetAttributeColor(SkillAttribute attribute)
        {
            return attribute switch
            {
                SkillAttribute.Fire => fireColor,
                SkillAttribute.Ice => iceColor,
                SkillAttribute.Thunder => thunderColor,
                _ => basicColor
            };
        }

        /// <summary>
        /// 데미지 텍스트 애니메이션 시작
        /// </summary>
        private void StartAnimation()
        {
            isAnimating = true;

            // 애니메이션 시퀀스 생성
            animationSequence = DOTween.Sequence();

            // 스케일 애니메이션 (펀치 효과)
            animationSequence.Append(transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));

            // 위로 이동하면서 페이드 아웃
            Vector3 targetPosition = startPosition + Vector3.up * moveDistance;
            animationSequence.Join(transform.DOMoveY(targetPosition.y, animationDuration).SetEase(Ease.OutCubic));
            animationSequence.Join(canvasGroup.DOFade(0f, animationDuration).SetEase(Ease.InCubic));

            // 좌우로 약간 흔들림 효과 (선택적)
            float shakeStrength = Random.Range(-30f, 30f);
            animationSequence.Join(transform.DOMoveX(startPosition.x + shakeStrength, animationDuration * 0.3f)
                .SetEase(Ease.OutCubic));

            // 애니메이션 완료 시 콜백
            animationSequence.OnComplete(() =>
            {
                isAnimating = false;
                OnAnimationComplete?.Invoke(this);
            });
        }

        /// <summary>
        /// 애니메이션 중지 및 정리
        /// </summary>
        public void StopAnimation()
        {
            if (animationSequence != null && animationSequence.IsActive())
            {
                animationSequence.Kill();
            }
            
            isAnimating = false;
            canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// 데미지 텍스트 리셋 (풀링용)
        /// </summary>
        public void ResetDamageText()
        {
            StopAnimation();
            transform.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
            
            if (damageText != null)
            {
                damageText.text = "";
                damageText.color = basicColor;
                damageText.fontSize = minFontSize;
            }
        }

        /// <summary>
        /// 현재 애니메이션 중인지 여부
        /// </summary>
        /// <returns>애니메이션 상태</returns>
        public bool IsAnimating()
        {
            return isAnimating;
        }

        /// <summary>
        /// 속성별 색상 미리보기 (에디터용)
        /// </summary>
        /// <param name="attribute">미리볼 속성</param>
        [ContextMenu("화염 색상 미리보기")]
        private void PreviewFireColor()
        {
            if (damageText != null)
            {
                damageText.color = fireColor;
                damageText.text = "FIRE 100";
            }
        }

        [ContextMenu("얼음 색상 미리보기")]
        private void PreviewIceColor()
        {
            if (damageText != null)
            {
                damageText.color = iceColor;
                damageText.text = "ICE 80";
            }
        }

        [ContextMenu("번개 색상 미리보기")]
        private void PreviewThunderColor()
        {
            if (damageText != null)
            {
                damageText.color = thunderColor;
                damageText.text = "THUNDER 120";
            }
        }

        private void OnDestroy()
        {
            // 트윈 정리
            if (animationSequence != null && animationSequence.IsActive())
            {
                animationSequence.Kill();
            }
        }
    }
} 
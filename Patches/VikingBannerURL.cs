using System;
using System.Collections;
using System.Linq;
using BepInEx;
using UnityEngine;
using UnityEngine.Networking;
using VikingBanners;
using VikingBanners.Compatibility.WardIsLove;

namespace VikingBanners.Patches
{
    public class VikingBannerURL : MonoBehaviour, Hoverable, Interactable, TextReceiver
    {
        /* Credit to RoloPogo for the original code this was worked off of. */

        private string m_url;
        public string m_name = "VikingBannerURL";
        private const int m_characterLimit = int.MaxValue;

        private MeshRenderer bannerRenderer;
        private SkinnedMeshRenderer raeBannerRenderer;
        private Texture origBannerTexture;
        private Texture origNormalMapTexture;
        private Texture origRaeBannerTexture;
        private Texture origRaeNormalMapTexture;

        private ZNetView m_nview;

        private void Awake()
        {
            TryGetComponent(out ZNetView? zNetView);
            if (zNetView == null) return;
            if (!zNetView.IsValid()) return;
            m_nview = zNetView;


            if (!Utils.GetPrefabName(gameObject).Contains("piece_banner"))
            {
                if (Utils.GetPrefabName(gameObject).Contains("piece_cloth_hanging_door"))
                {
                    bannerRenderer = GetComponentsInChildren<MeshRenderer>()
                        .FirstOrDefault(x => x?.name == "banner");
                }
                else if (Utils.GetPrefabName(gameObject).Contains("rae_ob_banner"))
                {
                    raeBannerRenderer = GetComponentsInChildren<SkinnedMeshRenderer>()
                        .FirstOrDefault(x => x?.name == "Banner");
                }
                else
                {
                    VikingBannersPlugin.VikingBannersLogger.LogError("VikingBannerURL: MeshRenderer not found.");
                }
            }
            else
            {
                bannerRenderer = GetComponentsInChildren<MeshRenderer>()
                    .FirstOrDefault(x => x?.name == "default");
            }

            try
            {
                if (raeBannerRenderer?.material != null)
                {
                    origRaeBannerTexture = raeBannerRenderer.material.GetTexture("_MainTex");
                    origRaeNormalMapTexture = raeBannerRenderer.material.GetTexture("_BumpMap");
                }
                
                if (bannerRenderer?.material != null)
                {
                    origBannerTexture = bannerRenderer.material.GetTexture("_MainTex");
                    origNormalMapTexture = bannerRenderer.material.GetTexture("_BumpMap");
                }
            }
            catch (Exception e)
            {
                VikingBannersPlugin.VikingBannersLogger.LogError($"Error getting texture: {e}");
            }

            if (m_nview?.GetZDO() != null)
            {
                InvokeRepeating("UpdateText", 2f, 2f);
                this.m_nview.Register<string>(nameof(GetNewImageAndApply),
                    new Action<long, string>(GetNewImageAndApply));
            }
        }


        public string GetHoverText()
        {
            if (VikingBannersPlugin.useServerBannerURL.Value == VikingBannersPlugin.Toggle.On)
            {
                return string.Empty;
            }

            if (!VikingBannersPlugin.AllowInput()) return string.Empty;

            if (VikingBannersPlugin.showURLOnHover.Value == VikingBannersPlugin.Toggle.On)
            {
                return Localization.instance.Localize(
                           $"{Environment.NewLine}[<color=#FFFF00><b>$KEY_Use</b></color>] $set_url") + "\n" +
                       GetText();
            }

            return Localization.instance.Localize(
                $"{Environment.NewLine}[<color=#FFFF00><b>$KEY_Use</b></color>] $set_url");
        }

        public string GetHoverName()
        {
            if (VikingBannersPlugin.useServerBannerURL.Value == VikingBannersPlugin.Toggle.On)
            {
                return string.Empty;
            }

            return !VikingBannersPlugin.AllowInput() ? string.Empty : Localization.instance.Localize("$banner_url");
        }

        public bool Interact(Humanoid character, bool hold, bool alt)
        {
            if (hold)
            {
                return false;
            }

            if (VikingBannersPlugin.useServerBannerURL.Value == VikingBannersPlugin.Toggle.On)
            {
                character?.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize(
                        $"<color=#FFFF00><b>$piece_noaccess</b></color> {Environment.NewLine} $server_banner_url_deny"));
                return false;
            }

            if (!VikingBannersPlugin.AllowInput()) return false;

            if (!PrivateArea.CheckAccess(transform.position, 0f, true))
            {
                character?.Message(MessageHud.MessageType.Center, "<color=#FFFF00><b>$piece_noaccess</b></color>");
                return false;
            }

            if (WardIsLovePlugin.IsLoaded())
            {
                if (WardIsLovePlugin.WardEnabled().Value &&
                    WardMonoscript.CheckInWardMonoscript(transform.position))
                {
                    if (!WardMonoscript.CheckAccess(transform.position, 0f, true))
                    {
                        // private zone
                        return false;
                    }
                }
            }

            TextInput.instance.RequestText(this, "$piece_sign_input", m_characterLimit);
            return true;
        }

        private void UpdateText()
        {
            string text = GetText();
            if (m_url == text)
            {
                return;
            }

            SetText(text);
        }

        public string GetText()
        {
            return VikingBannersPlugin.useServerBannerURL.Value == VikingBannersPlugin.Toggle.On
                ? VikingBannersPlugin.serverBannerURL.Value
                : m_nview.GetZDO().GetString("VikingBannerURL", string.Empty);
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        public void SetText(string text)
        {
            if (!PrivateArea.CheckAccess(transform.position, 0f, true))
            {
                return;
            }

            if (WardIsLovePlugin.IsLoaded())
            {
                if (WardIsLovePlugin.WardEnabled().Value &&
                    WardMonoscript.CheckInWardMonoscript(transform.position))
                {
                    if (!WardMonoscript.CheckAccess(transform.position, 0f, true))
                    {
                        // private zone
                        return;
                    }
                }
            }

            m_nview.InvokeRPC(ZNetView.Everybody, nameof(GetNewImageAndApply), text);
        }

        private bool IsRaeBanner()
        {
            return Utils.GetPrefabName(gameObject).Contains("rae_ob_banner");
        }

        private void GetNewImageAndApply(long uid, string url)
        {
            StartCoroutine(DownloadTexture(url, ApplyTexture));
        }

        private void ApplyTexture(string url, Texture2D obj)
        {
            if (!m_nview.HasOwner())
            {
                m_nview.ClaimOwnership();
                UpdateTexture(url, obj);
            }
            else
            {
                UpdateTexture(url, obj);
            }
        }

        private void UpdateTexture(string url, Texture2D obj)
        {
            if (m_nview.IsOwner())
            {
                m_nview.GetZDO().Set("VikingBannerURL", url);
            }

            if (IsRaeBanner())
            {
                raeBannerRenderer.material.SetTexture("_MainTex",
                    string.IsNullOrWhiteSpace(url) ? origRaeBannerTexture : obj);
                NullOutNormalMap(url, obj, true);
            }
            else
            {
                bannerRenderer.material.SetTexture("_MainTex",
                    string.IsNullOrWhiteSpace(url) ? origBannerTexture : obj);
                NullOutNormalMap(url, obj);
            }
        }

        private void NullOutNormalMap(string url, Texture2D obj, bool isRaeBanner = false)
        {
            if (isRaeBanner)
            {
                raeBannerRenderer.material.SetTexture("_BumpMap",
                    string.IsNullOrWhiteSpace(url) ? origRaeNormalMapTexture : obj);
            }
            else
            {
                bannerRenderer.material.SetTexture("_BumpMap",
                    string.IsNullOrWhiteSpace(url) ? origNormalMapTexture : obj);
            }
        }

        public IEnumerator DownloadTexture(string url, Action<string, Texture2D> callback)
        {
            m_url = url;
            if (m_url.IsNullOrWhiteSpace())
            {
                callback.Invoke(url, null!);
                yield break;
            }

            using UnityWebRequest uwr = UnityWebRequest.Get(url);
            yield return uwr.SendWebRequest();
            if (uwr.isNetworkError || uwr.isHttpError)
            {
                VikingBannersPlugin.VikingBannersLogger.LogError(uwr.error + Environment.NewLine + url);
            }
            else
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(uwr.downloadHandler.data);
                callback.Invoke(url, tex);
            }
        }
    }
}
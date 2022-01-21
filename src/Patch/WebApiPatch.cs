﻿using PeroTools2.Commons;
using Newtonsoft.Json.Linq;
using IL2CppJson = Il2CppNewtonsoft.Json.Linq;
using System;
using Account;
using UnityEngine.Networking;
using static PeroTools2.Commons.WebUtils;
using Il2CppSystem.Collections.Generic;
using UnhollowerRuntimeLib;
using UnhollowerBaseLib;
using HarmonyLib;
using System.Runtime.InteropServices;
using System.Linq;
#if MELON
using MelonLoader;
#endif
namespace CustomAlbums.Patch
{
    class WebApiPatch
    {
        private static readonly Logger Log = new Logger("WebApiPatch");

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr SendToUrlDelegate(
            IntPtr hiddenStructReturn,
            IntPtr thisPtr,
            IntPtr url,
            IntPtr method,
            IntPtr datas,
            IntPtr succeedCallback,
            IntPtr failCallback,
            IntPtr startCallback,
            IntPtr completeCallback,
            IntPtr headers,
            IntPtr nativeMethodInfo
            );
        private static SendToUrlDelegate OriginalSendToUrl;

        unsafe public static void DoPatching()
        {
            var methodPtr = Utils.NativeMethod(typeof(GameAccountSystem), nameof(GameAccountSystem.SendToUrl));
            var methodPatchPtr = AccessTools.Method(typeof(WebApiPatch), nameof(WebApiPatch.SendToUrlPatch)).MethodHandle.GetFunctionPointer();
#if BEPINEX
            Log.Debug("Not implemented yet");
#elif MELON
            MelonUtils.NativeHookAttach((IntPtr)(&methodPtr), methodPatchPtr);
            OriginalSendToUrl = Marshal.GetDelegateForFunctionPointer<SendToUrlDelegate>(methodPtr);
#endif
        }
        /// <summary>
        /// Hook GameAccountSystem request.
        /// </summary>
        public static IntPtr SendToUrlPatch(
            IntPtr hiddenStructReturn,
            IntPtr thisPtr,
            IntPtr url,
            IntPtr method,
            IntPtr datas,
            IntPtr succeedCallback,
            IntPtr failCallback,
            IntPtr startCallback,
            IntPtr completeCallback,
            IntPtr headers,
            IntPtr nativeMethodInfo
            )
        {
            bool blockThisRequest = false;
            var _url = IL2CPP.Il2CppStringToManaged(url);
            var _method = IL2CPP.Il2CppStringToManaged(method);
            Dictionary<string, Il2CppSystem.Object> _datas = null;
            Il2CppSystem.Action<IL2CppJson.JObject> _succeedCallback = null;
            Il2CppSystem.Action<IL2CppJson.JObject> _failCallback = null;

            // Convert PtrInt to Il2Cpp object
            if (datas != IntPtr.Zero)
                _datas = new Dictionary<string, Il2CppSystem.Object>(datas);
            if (succeedCallback != IntPtr.Zero)
                _succeedCallback = new Il2CppSystem.Action<IL2CppJson.JObject>(succeedCallback);
            if (failCallback != IntPtr.Zero)
                _failCallback = new Il2CppSystem.Action<IL2CppJson.JObject>(failCallback);

            
            // Store original callback reference
            var originalSucceedCallback = _succeedCallback;
            var originalFailCallback = _failCallback;

            Log.Debug($"[SendToUrlPatch] url:{_url} method:{_method}");

            switch (_url)
            {
                // Add custom tag.
                case "musedash/v1/music_tag":
                    _succeedCallback = new Action<IL2CppJson.JObject>(jObject =>
                    {
                        Log.Debug("Injected: musedash/v1/music_tag");
                        var JObj = jObject.IL2CppJsonSerialize().JsonDeserialize<JObject>();
                        var jArray = JObj["music_tag_list"];
                        var music_tag = jArray.First(o => o["sort_key"].Value<int>() == 8);

                        music_tag["tag_name"] = JObject.FromObject(AlbumManager.Langs);
                        music_tag["tag_picture"] = "https://mdmc.moe/cdn/melon.png";
                        music_tag["icon_name"] = "";
                        music_tag["music_list"] = JArray.FromObject(AlbumManager.GetAllUid());

                        var newJObject = JObj.JsonSerialize().IL2CppJsonDeserialize<IL2CppJson.JObject>();
                        originalSucceedCallback?.Invoke(newJObject);
                    });
                    OriginalSendToUrl(hiddenStructReturn, thisPtr, url, method, datas, _succeedCallback.Pointer, failCallback, startCallback, completeCallback, headers, nativeMethodInfo);
                    break;
            }

            if (!blockThisRequest)
                return OriginalSendToUrl(hiddenStructReturn, thisPtr, url, method, datas, succeedCallback, failCallback, startCallback, completeCallback, headers, nativeMethodInfo);
            // Request blocked
            return IntPtr.Zero;
        }
        public static bool SendToUrlPrefix(
            ref string url,
            ref string method, 
            ref Dictionary<string, Il2CppSystem.Object> datas, 
            ref Il2CppSystem.Action<IL2CppJson.JObject> succeedCallback , 
            ref Il2CppSystem.Action<long, string> faillCallback,
            ref Il2CppSystem.Action startCallback,
            ref Il2CppSystem.Action completeCallback,
            ref Dictionary<string, string> headers
            )
        {
            Log.Debug($"request: {url}");
            return true;
        }
        /// <summary>
        /// Hook all request.
        /// </summary>
        /// <returns></returns>
        //[HarmonyPatch(typeof(WebUtils), nameof(WebUtils.SendToUrl), new Type[] { typeof(PeroWebRequest) })]
        //[HarmonyPostfix]
        public static void SendToUrlPrefix2(PeroWebRequest webRequest)
        {
            Log.Debug($"Incoming request:{webRequest.method} {webRequest.url}");

            Log.Debug($"Request:{webRequest.method} {webRequest.url} headers:{webRequest.headers?.JsonSerialize()} datas:{webRequest.datas?.JsonSerialize()}");
            
            var originSuccessCallback = webRequest.succeedCallback;
            var originFailCallback = webRequest.faillCallback;

            webRequest.succeedCallback = new Action<DownloadHandler>(handler =>
            {
                Log.Debug($"Response:{webRequest.method} {webRequest.url} body:{handler.text}");
                originSuccessCallback?.Invoke(handler);
            });
            webRequest.faillCallback = new Action<long, string>((code, error) =>
            {
                Log.Debug($"Failed:{webRequest.method} {webRequest.url} code:{code} error:{error}");
                originFailCallback?.Invoke(code, error);
            });
        }
    }
}

using UnityEngine;
using System.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;

#pragma warning disable CS0649

public class Purchases : MonoBehaviour
{
    public abstract class Listener : MonoBehaviour
    {
        public abstract void ProductsReceived(List<Product> products);
        public abstract void PurchaseCompleted(string productIdentifier, Error error, PurchaserInfo purchaserInfo, bool userCanceled);
        public abstract void PurchaserInfoReceived(PurchaserInfo purchaserInfo);
    }

    /*
     * PurchaserInfo encapsulate the current status of subscriber. 
     * Use it to determine which entitlements to unlock, typically by checking 
     * ActiveSubscriptions or via LatestExpirationDate. 
     * 
     * Note: All DateTimes are in UTC, be sure to compare them with 
     * DateTime.UtcNow
     */
    public class PurchaserInfo
    {
        private PurchaserInfoResponse response;
        public PurchaserInfo(PurchaserInfoResponse response)
        {
            this.response = response;
        }


        public List<string> ActiveSubscriptions
        {
            get
            {
                return response.activeSubscriptions;
            }
        }

        public List<string> AllPurchasedProductIdentifiers
        {
            get
            {
                return response.allPurchasedProductIdentifiers;
            }
        }

        public DateTime LatestExpirationDate
        {
            get
            {
				return FromUnixTime(response.latestExpirationDate);
            }
        }

		private static DateTime FromUnixTime(long unixTime)
        {
            return epoch.AddSeconds(unixTime);
        }
		private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public Dictionary<string, DateTime> AllExpirationDates
        {
            get
            {
                Dictionary<string, DateTime> allExpirations = new Dictionary<string, DateTime>();
                for (int i = 0; i < response.allExpirationDateKeys.Count; i++)
                {
					var date = FromUnixTime(response.allExpirationDateValues[i]);
                    if (date != null)
                    {
                        allExpirations[response.allExpirationDateKeys[i]] = date;
                    }
                }
                return allExpirations;
            }
        }

    }

    [Serializable]
    public class Error
    {
        public string message;
        public int code;
        public string domain;
    }

    [Serializable]
    public class Product
    {
        public string title;
        public string identifier;
        public string description;
        public float price;
        public string priceString;
    }

    [Tooltip("Your RevenueCat API Key. Get from https://app.revenuecat.com/")]
    public string revenueCatAPIKey;

    [Tooltip("App user id. Pass in your own ID if your app has accounts. If blank, RevenueCat will generate a user ID for you.")]
    public string appUserID;

    [Tooltip("List of product identifiers.")]
    public string[] productIdentifiers;

    [Tooltip("A subclass of Purchases.Listener component. Use your custom subclass to define how to handle events.")]
    public Listener listener;

    private PurchasesWrapper wrapper;

    void Start()
    {
        string appUserID = (this.appUserID.Length == 0) ? null : this.appUserID;

#if UNITY_ANDROID && !UNITYEDITOR
        this.wrapper = new PurchasesWrapperAndroid();
#elif UNITY_IPHONE && !UNITYEDITOR
        this.wrapper = new PurchasesWrapperiOS();
#endif

        this.wrapper.Setup(gameObject.name, revenueCatAPIKey, appUserID);
        this.wrapper.GetProducts(productIdentifiers);
    }

    // Call this to initialte a purchase
    public void MakePurchase(string productIdentifier, string[] oldSku, string type = "subs")
    {
#if UNITY_ANDROID
        ((this.wrapper) as PurchasesWrapperAndroid).MakePurchase(productIdentifier, oldSku, type);
#else
        this.MakePurchase(productIdentifier, type);
#endif
    }
    
    // Call this to initialte a purchase
    public void MakePurchase(string productIdentifier, string type = "subs")
    {
        this.wrapper.MakePurchase(productIdentifier, type);
    }

    [Serializable]
    private class ProductResponse
    {
        public List<Product> products;
    }

    private void _receiveProducts(string productsJSON)
    {
        ProductResponse response = JsonUtility.FromJson<ProductResponse>(productsJSON);
        listener.ProductsReceived(response.products);
    }

    [Serializable]
    private class ReceivePurchaserInfoResponse
    {
        public string productIdentifier;
        public PurchaserInfoResponse purchaserInfo;
        public Error error;
    }

    [Serializable]
    public class PurchaserInfoResponse
    {
        public List<string> activeSubscriptions;
        public List<string> allPurchasedProductIdentifiers;
        public long latestExpirationDate;
        public List<string> allExpirationDateKeys;
        public List<long> allExpirationDateValues;
    }

    private void _receivePurchaserInfo(string arguments)
    {
        var response = JsonUtility.FromJson<ReceivePurchaserInfoResponse>(arguments);

        var error = (response.error.message != null) ? response.error : null;
        var info = (response.purchaserInfo.activeSubscriptions != null) ? new PurchaserInfo(response.purchaserInfo) : null;

#if UNITY_ANDROID
        if (error != null)
        {
            if (error != null && error.domain.Equals("1") && error.code == 1)
            {
                // send user cancelled message to the application
                listener.PurchaseCompleted(null, null, null, true);
            }
            else
            {
                // send error message to the application
                listener.PurchaseCompleted(response.productIdentifier, error, info, false);
            }
        }
        else
#endif
        if (response.productIdentifier != null)
        {
#if UNITY_ANDROID
                bool userCanceled = (error != null && error.domain.Equals("1") && error.code == 1);
#else
                bool userCanceled = (error != null && error.domain == "SKErrorDomain" && error.code == 2);
#endif

            listener.PurchaseCompleted(response.productIdentifier, error, info, userCanceled);
        }
        else if (info != null)
        {
            listener.PurchaserInfoReceived(info);
        }
    }
}

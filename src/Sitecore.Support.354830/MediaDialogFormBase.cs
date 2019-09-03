namespace Sitecore.Support.XA.Foundation.Multisite.Dialogs
{
    using Microsoft.Extensions.DependencyInjection;
    using Sitecore.Data.Items;
    using Sitecore.DependencyInjection;
    using Sitecore.Text;
    using Sitecore.Web.UI;
    using Sitecore.Web.UI.HtmlControls;
    using Sitecore.XA.Foundation.Multisite;
    using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
    using System.Linq;
    using System.Web;

    public class MediaDialogFormBase : Sitecore.XA.Foundation.Multisite.Dialogs.MediaDialogFormBase
    {
        protected new bool SetupVirtualMediaUse(DataContext context, WebControl tab)
        {
            Item folder = context.GetFolder();
            Item rootItem = ServiceLocator.ServiceProvider.GetService<ISiteMediaRootProvider>().GetRootItem(folder.Database);
            if (rootItem != null)
            {
                context.Root = rootItem.ID.ToString();
                VirtualMediaUsed = true;
                tab.AddParameter("id", rootItem.ID.ToShortID().ToString().ToLowerInvariant());
                #region Removed code
                //Frame obj = (Frame)tab.Controls[0];
                //string value = HttpUtility.UrlEncode(rootItem.ID.ToString());
                //UrlString urlString = new UrlString(obj.SourceUri);
                //if (urlString.Parameters.AllKeys.Contains("id"))
                //{
                //    urlString.Parameters["id"] = value;
                //}
                //else
                //{
                //    urlString.Parameters.Add("id", value);
                //}
                //obj.SourceUri = urlString.ToString();
                #endregion
            }
            return VirtualMediaUsed;
        }
    }
}
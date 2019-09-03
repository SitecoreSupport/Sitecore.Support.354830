namespace Sitecore.Support.XA.Foundation.Multisite.Dialogs
{
    using Sitecore;
    using Sitecore.Buckets.Controls;
    using Sitecore.Configuration;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Resources;
    using Sitecore.Resources.Media;
    using Sitecore.Shell;
    using Sitecore.Shell.Framework;
    using Sitecore.StringExtensions;
    using Sitecore.Text;
    using Sitecore.Web;
    using Sitecore.Web.UI.HtmlControls;
    using Sitecore.Web.UI.Sheer;
    using Sitecore.Web.UI.WebControls;
    using Sitecore.XA.Foundation.Multisite.Dialogs;
    using System;
    using System.Collections.Specialized;
    using System.Drawing;
    using System.IO;
    using System.Web;
    using System.Web.UI;

    public class InsertImageForm : Sitecore.Support.XA.Foundation.Multisite.Dialogs.MediaDialogFormBase
    {
        protected DataContext DataContext;

        protected Edit Filename;

        protected Scrollbox Listview;

        protected TreeviewEx Treeview;

        protected Button Upload;

        protected SearchTab SearchMediaLibraryTab;

        protected Language ContentLanguage
        {
            get
            {
                if (!Language.TryParse(WebUtil.GetQueryString("la"), out Language result))
                {
                    return base.Context.ContentLanguage;
                }
                return result;
            }
        }

        protected string Mode
        {
            get
            {
                return Assert.ResultNotNull(StringUtil.GetString(ServerProperties["Mode"], "shell"));
            }
            set
            {
                Assert.ArgumentNotNull(value, "value");
                ServerProperties["Mode"] = value;
            }
        }

        protected bool UploadButtonDisabled
        {
            get
            {
                bool result;
                return bool.TryParse(StringUtil.GetString(ServerProperties["UploadButtonDisabled"], "false"), out result) && result;
            }
            set
            {
                if (value != UploadButtonDisabled)
                {
                    ServerProperties["UploadButtonDisabled"] = value;
                    string text = "var uploadButton = document.getElementById(\"{0}\");\r\n                                    if (uploadButton){{\r\n                                        uploadButton.disabled = {1};\r\n                                    }}".FormatWith(Upload.UniqueID, value.ToString().ToLowerInvariant());
                    if (base.Context.Page.Page.IsPostBack)
                    {
                        SheerResponse.Eval(text);
                    }
                    else
                    {
                        base.Context.Page.Page.ClientScript.RegisterStartupScript(GetType(), "UploadButtonModification", text, addScriptTags: true);
                    }
                }
            }
        }

        protected void Edit()
        {
            Item selectionItem = Treeview.GetSelectionItem();
            if (selectionItem == null || selectionItem.TemplateID == TemplateIDs.MediaFolder || selectionItem.TemplateID == TemplateIDs.MainSection)
            {
                SheerResponse.Alert("Select a media item.");
                return;
            }
            UrlString urlString = new UrlString("/sitecore/shell/Applications/Content Manager/default.aspx");
            urlString["fo"] = selectionItem.ID.ToString();
            urlString["mo"] = "popup";
            urlString["wb"] = "0";
            urlString["pager"] = "0";
            urlString[State.Client.UsesBrowserWindowsQueryParameterName] = "1";
            base.Context.ClientPage.ClientResponse.ShowModalDialog(urlString.ToString(), string.Equals(base.Context.Language.Name, "ja-jp", StringComparison.InvariantCultureIgnoreCase) ? "1115" : "955", "560");
        }

        private Item GetCurrentItem(Message message)
        {
            return GetCurrentItem(DataContext, message, DataContext.Language);
        }

        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            if (message.Name == "item:load")
            {
                LoadItem(message);
                return;
            }
            Dispatcher.Dispatch(message, GetCurrentItem(message));
            base.HandleMessage(message);
        }

        protected void Listview_Click(string id)
        {
            Assert.ArgumentNotNullOrEmpty(id, "id");
            Item item = Client.ContentDatabase.GetItem(id, ContentLanguage);
            if (item != null)
            {
                SelectItem(item);
            }
        }

        private void LoadItem(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            Language language = DataContext.Language;
            Item folder = DataContext.GetFolder();
            if (folder != null)
            {
                language = folder.Language;
            }
            Item item = Client.ContentDatabase.GetItem(ID.Parse(message["id"]), language);
            if (item != null)
            {
                SelectItem(item);
            }
        }

        protected override void OnCancel(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (Mode == "webedit")
            {
                base.OnCancel(sender, args);
            }
            else
            {
                SheerResponse.Eval("scCancel()");
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            if (!base.Context.ClientPage.IsEvent)
            {
                Mode = WebUtil.GetQueryString("mo");
                DataContext.GetFromQueryString();
                string queryString = WebUtil.GetQueryString("fo");
                if (ShortID.IsShortID(queryString))
                {
                    queryString = ShortID.Parse(queryString).ToID().ToString();
                    DataContext.Folder = queryString;
                }
                base.Context.ClientPage.ServerProperties["mode"] = WebUtil.GetQueryString("mo");
                if (!string.IsNullOrEmpty(WebUtil.GetQueryString("databasename")))
                {
                    DataContext.Parameters = "databasename=" + WebUtil.GetQueryString("databasename");
                }
                Item folder = DataContext.GetFolder();
                if (SetupVirtualMediaUse(DataContext, SearchMediaLibraryTab) && folder.ID == ItemIDs.MediaLibraryRoot)
                {
                    folder = DataContext.GetFolder();
                }
                Assert.IsNotNull(folder, "Folder not found");
                SelectItem(folder);
                Upload.Click = "media:upload(edit=" + (Settings.Media.OpenContentEditorAfterUpload ? "1" : "0") + ",load=1)";
            }
        }

        protected override void OnOK(object sender, EventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            string value = Filename.Value;
            if (value.Length == 0)
            {
                SheerResponse.Alert("Select a media item.");
                return;
            }
            MediaItem mediaItem = GetSelectedItem(DataContext, value);
            if (mediaItem == null)
            {
                SheerResponse.Alert("The media item could not be found.");
                return;
            }
            if (!(MediaManager.GetMedia(MediaUri.Parse(mediaItem)) is ImageMedia))
            {
                SheerResponse.Alert("The selected item is not an image. Select an image to continue.");
                return;
            }
            MediaUrlOptions shellOptions = MediaUrlOptions.GetShellOptions();
            shellOptions.Language = ContentLanguage;
            string text = (!string.IsNullOrEmpty(HttpContext.Current.Request.Form["AlternateText"])) ? HttpContext.Current.Request.Form["AlternateText"] : mediaItem.Alt;
            Tag tag = new Tag("img");
            SetDimensions(mediaItem, shellOptions, tag);
            tag.Add("Src", MediaManager.GetMediaUrl(mediaItem, shellOptions));
            tag.Add("Alt", StringUtil.EscapeQuote(text));
            tag.Add("_languageInserted", "true");
            if (Mode == "webedit")
            {
                SheerResponse.SetDialogValue(StringUtil.EscapeJavascriptString(tag.ToString()));
                base.OnOK(sender, args);
            }
            else
            {
                SheerResponse.Eval("scClose(" + StringUtil.EscapeJavascriptString(tag.ToString()) + ")");
            }
        }

        private static void RenderEmpty(HtmlTextWriter output)
        {
            Assert.ArgumentNotNull(output, "output");
            output.Write("<table width=\"100%\" border=\"0\"><tr><td align=\"center\">");
            output.Write("<div style=\"padding:8px\">");
            output.Write(Translate.Text("This folder is empty."));
            output.Write("</div>");
            output.Write("</td></tr></table>");
        }

        private static void RenderListviewItem(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            MediaItem item2 = item;
            output.Write("<a href=\"#\" class=\"scTile\" onclick=\"javascript:return scForm.postEvent(this,event,'Listview_Click(&quot;" + item.ID + "&quot;)')\" >");
            output.Write("<div class=\"scTileImage\">");
            if (item.TemplateID == TemplateIDs.Folder || item.TemplateID == TemplateIDs.TemplateFolder || item.TemplateID == TemplateIDs.MediaFolder || MediaDialogFormBase.IsMediaVirtualFolder(item))
            {
                ImageBuilder imageBuilder = new ImageBuilder();
                imageBuilder.Src = item.Appearance.Icon;
                imageBuilder.Width = 48;
                imageBuilder.Height = 48;
                imageBuilder.Margin = "24px 24px 24px 24px";
                imageBuilder.Render(output);
            }
            else
            {
                MediaUrlOptions shellOptions = MediaUrlOptions.GetShellOptions();
                shellOptions.AllowStretch = false;
                shellOptions.BackgroundColor = Color.White;
                shellOptions.Language = item.Language;
                shellOptions.Thumbnail = true;
                shellOptions.UseDefaultIcon = true;
                shellOptions.Width = 96;
                shellOptions.Height = 96;
                output.Write("<img src=\"" + MediaManager.GetMediaUrl(item2, shellOptions) + "\" class=\"scTileImageImage\" border=\"0\" alt=\"\" />");
            }
            output.Write("</div>");
            output.Write("<div class=\"scTileHeader\">");
            output.Write(item.DisplayName);
            output.Write("</div>");
            output.Write("</a>");
        }

        private static void RenderPreview(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            MediaItem mediaItem = item;
            MediaUrlOptions shellOptions = MediaUrlOptions.GetShellOptions();
            shellOptions.AllowStretch = false;
            shellOptions.BackgroundColor = Color.White;
            shellOptions.Language = item.Language;
            shellOptions.Thumbnail = true;
            shellOptions.UseDefaultIcon = true;
            shellOptions.Width = 192;
            shellOptions.Height = 192;
            string mediaUrl = MediaManager.GetMediaUrl(mediaItem, shellOptions);
            output.Write("<table width=\"100%\" height=\"100%\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\">");
            output.Write("<tr><td align=\"center\" height=\"100%\">");
            output.Write("<div class=\"scPreview\">");
            output.Write("<img src=\"" + mediaUrl + "\" class=\"scPreviewImage\" border=\"0\" alt=\"\" />");
            output.Write("</div>");
            output.Write("<div class=\"scPreviewHeader\">");
            output.Write(item.DisplayName);
            output.Write("</div>");
            output.Write("</td></tr>");
            if (!(MediaManager.GetMedia(MediaUri.Parse(mediaItem)) is ImageMedia))
            {
                output.Write("</table>");
                return;
            }
            output.Write("<tr><td class=\"scProperties\">");
            output.Write("<table border=\"0\" cellpadding=\"2\" cellspacing=\"0\">");
            output.Write("<col align=\"right\" />");
            output.Write("<col align=\"left\" />");
            output.Write("<tr><td>");
            output.Write(Translate.Text("Alternate text:"));
            output.Write("</td><td>");
            output.Write("<input type=\"text\" id=\"AlternateText\" value=\"{0}\" />", HttpUtility.HtmlEncode(mediaItem.Alt));
            output.Write("</td></tr>");
            output.Write("<tr><td>");
            output.Write(Translate.Text("Width:"));
            output.Write("</td><td>");
            output.Write("<input type=\"text\" id=\"Width\" value=\"{0}\" />", HttpUtility.HtmlEncode(mediaItem.InnerItem["Width"]));
            output.Write("</td></tr>");
            output.Write("<tr><td>");
            output.Write(Translate.Text("Height:"));
            output.Write("</td><td>");
            output.Write("<input type=\"text\" id=\"Height\" value=\"{0}\" />", HttpUtility.HtmlEncode(mediaItem.InnerItem["Height"]));
            output.Write("</td></tr>");
            output.Write("</table>");
            output.Write("</td></tr>");
            output.Write("</table>");
            SheerResponse.Eval("scAspectPreserver.reload();");
        }

        private void SelectItem(Item item, bool expand = true)
        {
            Assert.ArgumentNotNull(item, "item");
            UploadButtonDisabled = (!item.Access.CanCreate() || (base.VirtualMediaUsed && DataContext.Root == item.ID.ToString()));
            Filename.Value = ShortenPath(item.Paths.Path);
            DataContext.SetFolder(item.Uri);
            FixInitialFolderSelection(DataContext, item);
            if (expand)
            {
                Treeview.SetSelectedItem(item);
            }
            HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new StringWriter());
            if (item.TemplateID == TemplateIDs.Folder || item.TemplateID == TemplateIDs.MediaFolder || item.TemplateID == TemplateIDs.MainSection || MediaDialogFormBase.IsMediaVirtualFolder(item))
            {
                foreach (Item child in GetChildren(item))
                {
                    if (child.Appearance.Hidden)
                    {
                        if (base.Context.User.IsAdministrator && UserOptions.View.ShowHiddenItems)
                        {
                            RenderListviewItem(htmlTextWriter, child);
                        }
                    }
                    else
                    {
                        RenderListviewItem(htmlTextWriter, child);
                    }
                }
            }
            else
            {
                RenderPreview(htmlTextWriter, item);
            }
            string text = htmlTextWriter.InnerWriter.ToString();
            if (string.IsNullOrEmpty(text))
            {
                RenderEmpty(htmlTextWriter);
                text = htmlTextWriter.InnerWriter.ToString();
            }
            Listview.InnerHtml = text;
        }

        protected void SelectTreeNode()
        {
            Item selectionItem = Treeview.GetSelectionItem(ContentLanguage, Sitecore.Data.Version.Latest);
            if (selectionItem != null)
            {
                SelectItem(selectionItem, expand: false);
            }
        }

        private void SetDimensions(MediaItem item, MediaUrlOptions options, Tag image)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(options, "options");
            Assert.ArgumentNotNull(image, "image");
            NameValueCollection form = HttpContext.Current.Request.Form;
            if (!string.IsNullOrEmpty(form["Width"]) && form["Width"] != item.InnerItem["Width"] && form["Height"] != item.InnerItem["Height"])
            {
                if (int.TryParse(form["Width"], out int result))
                {
                    options.Width = result;
                    image.Add("width", result.ToString());
                }
                if (int.TryParse(form["Height"], out int result2))
                {
                    options.Height = result2;
                    image.Add("height", result2.ToString());
                }
            }
            else
            {
                image.Add("width", item.InnerItem["Width"]);
                image.Add("height", item.InnerItem["Height"]);
            }
        }

        private string ShortenPath(string path)
        {
            return ShortenPath(DataContext, path);
        }
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using System.ComponentModel;
using Shell32;

namespace TaskTrayShortcuts
{
    public class TaskTrayShortcutsContext : ApplicationContext
    {
        string folderPath = null;
        NotifyIcon notifyIcon = new NotifyIcon();
        Configuration configWindow = new Configuration();

        public TaskTrayShortcutsContext(string[] args)
        {
            #if DEBUG
                this.folderPath = @"D:\_";
            #else
                // We expect a folder path as first argument
                if (args.Length == 0)
                {
                    MessageBox.Show("Missing parameter.");
                    System.Environment.Exit(1);
                }

                this.folderPath = args[0];
                if (!Directory.Exists(this.folderPath))
                {
                    MessageBox.Show("Path does not exists.");
                    System.Environment.Exit(1);
                }
            #endif


            List<ToolStripMenuItem> items = this.ProcessDirectory(this.folderPath, true);
            System.Drawing.Icon test = IconExtractor.Extract("shell32.dll", 131, true);
            items.Add(new ToolStripMenuItem("Exit", test.ToBitmap(), new EventHandler(Exit)));

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.AddRange(items.ToArray());

            notifyIcon.Icon = TaskTrayShortcuts.Properties.Resources.AppIcon;
            notifyIcon.DoubleClick += new EventHandler(Open);
            notifyIcon.Click += new EventHandler(Open);
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Visible = true;
        }

        // Process all files in the directory passed in, recurse on any directories
        // that are found, and process the files they contain.
        public List<ToolStripMenuItem> ProcessDirectory(string targetDirectory, bool addOpenFolderLink)
        {
            List<ToolStripMenuItem> menu = new List<ToolStripMenuItem>();
            ToolStripMenuItem item;
            FileInfo fInfo;

            System.Drawing.Bitmap folderImage = IconExtractor.Extract("shell32.dll", 3, true).ToBitmap();

            try
            {
                if (addOpenFolderLink)
                {
                    fInfo = new FileInfo(targetDirectory);
                    item = new ToolStripMenuItem("Open folder \"" + fInfo.Name + "\"", null, delegate (object sender, EventArgs e)
                    {
                        this.ExecuteCommand(sender, e, targetDirectory);
                    });
                    item.Image = IconExtractor.Extract("shell32.dll", 3, true).ToBitmap();
                    menu.Add(item);
                }

                // Recurse into subdirectories of this directory.
                string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
                foreach (string subdirectory in subdirectoryEntries)
                {
                    fInfo = new FileInfo(subdirectory);
                    item = new ToolStripMenuItem(fInfo.Name, null, ProcessDirectory(subdirectory, true).ToArray());
                    item.Image = folderImage;
                    menu.Add(item);
                }

                // Process the list of files found in the directory.
                string[] fileEntries = Directory.GetFiles(targetDirectory);
                foreach (string fileName in fileEntries)
                {
                    if (!fileName.ToLower().StartsWith("$"))
                    {
                        fInfo = new FileInfo(fileName);
                        if (!fInfo.Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            // Extract shortcut name and icon
                            string name = fInfo.Name.Substring(0, fInfo.Name.LastIndexOf(fInfo.Extension));

                            item = new ToolStripMenuItem(name, null, delegate (object sender, EventArgs e) {
                                this.ExecuteCommand(sender, e, fileName);
                            });

                            try
                            {
                                string target = GetShortcutTargetFile(fileName);
                                item.Image = GetShortcutTargetIcon(fileName, target);

                            }
                            catch (System.IO.FileNotFoundException) { }

                            menu.Add(item);
                        }
                    }
                }
            } catch (DirectoryNotFoundException) { }

            return menu;
        }

        public static string GetShortcutTargetFile(string shortcutFilename)
        {
            try
            {
                string pathOnly = Path.GetDirectoryName(shortcutFilename);
                string filenameOnly = Path.GetFileName(shortcutFilename);

                Shell shell = new Shell();
                Folder folder = shell.NameSpace(pathOnly);
                FolderItem folderItem = folder.ParseName(filenameOnly);
                if (folderItem != null)
                {
                    ShellLinkObject link = (ShellLinkObject)folderItem.GetLink;
                    return (link.Path == string.Empty ? shortcutFilename : link.Path);
                }
            }
            catch (System.NotImplementedException) { }
            
            return shortcutFilename;
        }

        public static System.Drawing.Bitmap GetShortcutTargetIcon(string shortcutFilename, string target)
        {
            System.Drawing.Icon icon = IconReader.GetFileIcon(target, IconReader.IconSize.Small, false);
            if (icon != null) return icon.ToBitmap();

            icon = IconReader.GetFileIcon(shortcutFilename, IconReader.IconSize.Small, false);
            if (icon != null) return icon.ToBitmap();

            icon = System.Drawing.Icon.ExtractAssociatedIcon(target);
            if (icon != null)
            {
                System.Drawing.Bitmap destinationImage = new System.Drawing.Bitmap(16, 16);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(icon.ToBitmap());
                System.Drawing.Rectangle destinationRect = new System.Drawing.Rectangle(0, 0, 16, 16);
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(destinationImage))
                {
                    //graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    //graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(bitmap, destinationRect, 0, 0, bitmap.Width, bitmap.Height, System.Drawing.GraphicsUnit.Pixel);
                }

                return destinationImage;
            }

            return System.Drawing.Icon.ExtractAssociatedIcon(shortcutFilename).ToBitmap();
        }

        void ExecuteCommand(object sender, EventArgs e, string fileName)
        {
            try
            {
                Process proc = new Process();
                proc.StartInfo.FileName = fileName;
                proc.Start();
            } catch (Win32Exception)
            {
                MessageBox.Show("Unable to start \"" + fileName + "\" process");
            }
        }

        void Open(object sender, EventArgs e)
        {
            MouseEventArgs me = (MouseEventArgs)e;
            if (me.Button is MouseButtons.Left)
            {
                if (notifyIcon.ContextMenuStrip.Visible)
                {
                    notifyIcon.ContextMenuStrip.Hide();
                }
                else
                {
                    System.Drawing.Point pos = Cursor.Position;
                    pos.X -= notifyIcon.ContextMenuStrip.Width;
                    pos.Y -= notifyIcon.ContextMenuStrip.Height;
                    if (pos.X < 0) pos.X = 0;
                    if (pos.Y < 0) pos.Y = 0;
                    notifyIcon.ContextMenuStrip.Show(pos);
                }
            }
        }

        void Exit(object sender, EventArgs e)
        {
            // We must manually tidy up and remove the icon before we exit.
            // Otherwise it will be left behind until the user mouses over.
            notifyIcon.Visible = false;
            Application.Exit();
        }
    }
}

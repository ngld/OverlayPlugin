using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using SharpCompress.Archives;

namespace RainbowMage.OverlayPlugin.Updater
{
    public class Installer
    {
        const uint FILE_OVERWRITE_RETRIES = 10;
        const int FILE_OVERWRITE_WAIT = 300;

        ProgressDisplay _display;
        public string TempDir {  get; private set; }
        string _destDir = null;
        CancellationToken _token = CancellationToken.None;

        public ProgressDisplay Display => _display;

        public Installer(string dest, string tmpName)
        {
            _display = new ProgressDisplay();
            _display.Show();

            _destDir = dest;
            // Make sure our temporary directory is on the same drive as the destination.
            TempDir = Path.Combine(Path.GetDirectoryName(dest), tmpName);
        }

        public static async Task<bool> Run(string url, string destDir, string tmpName, int stripDirs = 0, bool overwrite = false)
        {
            var inst = new Installer(destDir, tmpName);

            return await Task.Run(() =>
            {
                var scVersion = Assembly.Load("SharpCompress").GetName().Version;
                inst._display.Log("Current SharpCompress version: " + scVersion.ToString());
                if (scVersion < Version.Parse("0.21.1"))
                {
                    inst._display.Log(Resources.SharpCompressOutdatedError);
                    inst._display.UpdateStatus(0, Resources.StatusError);
                    return false;
                }

                var result = false;
                var archivePath = Path.Combine(inst.TempDir, "update.file");
                var dlResult = true;

                // Only try to download URLs. We can skip this step for local files.
                if (File.Exists(url))
                {
                    archivePath = url;
                } else
                {
                    dlResult = inst.Download(url, archivePath);
                }

                if (dlResult && inst.Extract(archivePath, stripDirs))
                {
                    result = overwrite ? inst.InstallOverwrite() : inst.InstallReplace();
                    inst.Cleanup();

                    if (result)
                    {
                        inst.Display.Close();
                    }
                }

                return result;
            });
        }

        public bool Download(string url, string dest, bool useHttpClient = false)
        {
            try
            {
                if (Directory.Exists(TempDir))
                {
                    Directory.Delete(TempDir, true);
                }

                Directory.CreateDirectory(TempDir);
            } catch (Exception ex)
            {
                _display.Log(string.Format(Resources.CreatingTempDirFailed, TempDir, ex));
                return false;
            }

            _display.UpdateStatus(0, string.Format(Resources.StatusDownloadStarted, 1, 2));

            // Avoid confusing users with the DO_NOT_DOWNLOAD extension. Users aren't supposed to manually download
            // these files from the GH releases page so I added that extension and didn't expect people to pay
            // attention to the download URL in the updater log.
            _display.Log(string.Format(Resources.LogDownloading, url.Replace(".DO_NOT_DOWNLOAD", ""), dest));

            var success = false;
            var cancel = _display.GetCancelToken();
            _token = cancel;

            try
            {
                var retries = 10;

                while (retries > 0 && !cancel.IsCancellationRequested)
                {
                    try
                    {
                        if (useHttpClient)
                        {
                            HttpClientWrapper.Get(url, new Dictionary<string, string>(), dest, DlProgressCallback, true);
                        } else { 
                            CurlWrapper.Get(url, new Dictionary<string, string>(), dest, DlProgressCallback, true);
                        }

                        success = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        _display.Log(string.Format(Resources.LogDownloadInterrupted, ex));

                        if (retries > 0 && !cancel.IsCancellationRequested)
                        {
                            // If this is a curl exception, it's most likely network related. Wait a second
                            // before trying again. We don't want to spam the other side with download requests.
                            if (ex.GetType() == typeof(CurlException))
                            {
                                if (!((CurlException) ex).Retry)
                                {
                                    // Retrying won't fix this kind of error. Abort.
                                    success = false;
                                    break;
                                }

                                Thread.Sleep(1000);
                            }

                            _display.Log(Resources.LogResumingDownload);
                            success = false;
                            continue;
                        }
                    }
                }

                if (cancel.IsCancellationRequested || !success)
                {
                    _display.UpdateStatus(0, Resources.StatusCancelling);
                    File.Delete(dest);

                    if (!cancel.IsCancellationRequested)
                    {
                        _display.UpdateStatus(0, Resources.OutOfRetries);
                        _display.Log(Resources.OutOfRetries);
                    }
                    else
                    {
                        _display.UpdateStatus(0, Resources.StatusCancelled);
                        _display.Log(Resources.LogAbortedByUser);
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                if (cancel.IsCancellationRequested)
                {
                    _display.UpdateStatus(1, Resources.StatusCancelled);
                    _display.Log(Resources.LogAbortedByUser);
                    return false;
                }

                _display.Log(string.Format(Resources.Exception, ex));
                return false;
            }
            finally
            {
                _display.DisposeCancelSource();

                if (!success) Cleanup();
            }

            return true;
        }

        private bool DlProgressCallback(long resumed, long dltotal, long dlnow, long ultotal, long ulnow)
        {
            var status = string.Format(Resources.StatusDownloadStarted, 1, 2);

            if (dltotal > 0)
                _display.UpdateStatus(((float)resumed + dlnow) / ((float)resumed + dltotal), status);

            return _token.IsCancellationRequested;
        }

        public bool Extract(string archivePath, int stripDirs = 0)
        {
            var success = false;
            var cancel = _display.GetCancelToken();

            try
            {
                _display.UpdateStatus(0, string.Format(Resources.StatusPreparingExtraction, 2, 2));
                _display.Log(Resources.LogOpeningArchive);

                var contentsPath = Path.Combine(TempDir, "contents");
                Directory.CreateDirectory(contentsPath);

                using (var archive = ArchiveFactory.Open(archivePath))
                {
                    var total = 0d;
                    var done = 0d;

                    foreach (var entry in archive.Entries)
                    {
                        total += entry.Size;
                    }

                    // Make sure we never divide by zero.
                    if (total == 0d) total = 1d;

                    using (var reader = archive.ExtractAllEntries())
                    {
                        reader.EntryExtractionProgress += (sender, e) =>
                        {
                            var percent = (float)(done + e.ReaderProgress.BytesTransferred) / total;

                            _display.UpdateStatus(Math.Min(percent, 1), $"[2/2]: {reader.Entry.Key}");
                        };

                        cancel = _display.GetCancelToken();
                        _display.Log(Resources.LogExtractingFiles);

                        while (reader.MoveToNextEntry())
                        {
                            if (cancel.IsCancellationRequested)
                            {
                                break;
                            }

                            var outPath = reader.Entry.Key;
                            if (stripDirs > 0)
                            {
                                var parts = outPath.Split('/');
                                if (parts.Length < stripDirs + 1)
                                {
                                    continue;
                                }
                                else
                                {
                                    outPath = string.Join("" + Path.DirectorySeparatorChar, parts.ToList().GetRange(stripDirs, parts.Length - stripDirs));
                                }
                            }

                            outPath = Path.Combine(contentsPath, outPath);

                            if (reader.Entry.IsDirectory)
                            {
                                Directory.CreateDirectory(outPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                                using (var writer = File.OpenWrite(outPath))
                                {
                                    reader.WriteEntryTo(writer);
                                }
                            }

                            done += reader.Entry.Size;
                        }
                    }
                }

                if (cancel.IsCancellationRequested)
                {
                    _display.UpdateStatus(1, Resources.StatusCancelled);
                    _display.Log(Resources.LogExtractionAbortedByUser);
                    return false;
                }

                success = true;
            } catch(Exception ex)
            {
                if (cancel.IsCancellationRequested)
                {
                    _display.UpdateStatus(1, Resources.StatusCancelled);
                    _display.Log(Resources.LogAbortedByUser);
                    return false;
                }

                _display.Log(string.Format(Resources.Exception, ex));
                return false;
            }
            finally
            {
                _display.DisposeCancelSource();

                if (!success) Cleanup();
            }

            _display.UpdateStatus(1, string.Format(Resources.StatusDone, 2, 2));
            _display.Log(Resources.LogDone);
            return success;
        }

        public bool InstallReplace()
        {
            try
            {
                string backup = null;
                var parent = Path.GetDirectoryName(_destDir);
                if (!Directory.Exists(parent))
                    Directory.CreateDirectory(parent);

                if (Directory.Exists(_destDir))
                {
                    _display.Log(Resources.LogBackingUpOldFiles);

                    backup = _destDir + ".bak";
                    if (Directory.Exists(backup))
                        Directory.Delete(backup, true);

                    Directory.Move(_destDir, backup);
                }

                try
                {
                    _display.Log(Resources.LogMovingDirectory);
                    Directory.Move(Path.Combine(TempDir, "contents"), _destDir);
                }
                catch (Exception e)
                {
                    _display.Log(string.Format(Resources.LogFailedReplaceDirectory, e));
                    _display.Log(Resources.LogCleaningUp);

                    if (Directory.Exists(_destDir))
                    {
                        Directory.Delete(_destDir, true);
                    }

                    if (backup != null)
                    {
                        _display.Log(Resources.LogRestoringBackup);

                        Directory.Move(backup, _destDir);
                    }

                    _display.Log(Resources.LogDone);
                    return false;
                }

                if (backup != null)
                {
                    _display.Log(Resources.LogRemovingOldBackup);
                    Directory.Delete(backup, true);
                }

                return true;
            }
            catch (Exception e)
            {
                _display.Log(string.Format(Resources.Exception, e));
                return false;
            }
        }

        public bool InstallOverwrite()
        {
            try
            {
                try
                {
                    _display.Log(Resources.LogOverwritingOldFiles);

                    var prefix = Path.Combine(TempDir, "contents");
                    var queue = new List<DirectoryInfo>() { new DirectoryInfo(prefix) };
                    while (queue.Count() > 0)
                    {
                        var info = queue[0];
                        queue.RemoveAt(0);

                        var sub = info.FullName.Substring(prefix.Length).TrimStart('\\', '/');
                        var sub_destDir = Path.Combine(_destDir, sub);
                        if (!Directory.Exists(sub_destDir))
                        {
                            Directory.CreateDirectory(sub_destDir);
                        }

                        foreach (var item in info.EnumerateDirectories())
                        {
                            queue.Add(item);
                        }

                        foreach (var item in info.EnumerateFiles())
                        {
                            bool done = false;
                            for (int i = 0; i < FILE_OVERWRITE_RETRIES; i++)
                            {
                                try
                                {
                                    File.Delete(Path.Combine(sub_destDir, item.Name));
                                    File.Move(item.FullName, Path.Combine(sub_destDir, item.Name));
                                    done = true;
                                    break;
                                } catch (Exception e)
                                {
                                    _display.Log(string.Format(Resources.LogOverwriteRetry, item.Name, e));
                                    Thread.Sleep(FILE_OVERWRITE_WAIT);
                                }
                            }

                            if (!done)
                            {
                                throw new Exception(Resources.LogOverwriteFailed);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _display.Log(string.Format(Resources.LogOverwritingOldFilesFailed, e));
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                _display.Log(string.Format(Resources.Exception, e));
                return false;
            }
        }

        public void Cleanup()
        {
            if (Directory.Exists(TempDir))
            {
                _display.Log(Resources.LogDeletingTempFiles);

                var retries = 10;
                while (retries > 0)
                {
                    retries--;
                    try
                    {
                        Directory.Delete(TempDir, true);
                        _display.Log(Resources.LogDone);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _display.Log(string.Format(Resources.LogFailedToDelete, TempDir, ex));
                        Thread.Sleep(300);
                    }
                }
            }
        }
    }
}

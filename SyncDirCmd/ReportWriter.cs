using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace SyncDirCmd
{
	class ReportWriter : IDisposable
	{
		private StreamWriter _rep = null;
		private bool _started = false;
		private bool _sectionHeaders = false;
		private bool _sectionContent = false;

		public ReportWriter(Stream stream)
		{
			if (stream == null) throw new ArgumentNullException();
			_rep = new StreamWriter(stream, Encoding.ASCII);
		}

		public ReportWriter(string fileName)
		{
			if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException();
			_rep = new StreamWriter(fileName, false, Encoding.ASCII);
		}

		public void Dispose()
		{
			Close();
		}

		public void Close()
		{
			if (_rep != null)
			{
				if (_started)
				{
					_rep.WriteLine("</body></html>");
				}
				_rep.Close();
				_rep = null;
			}
		}

		public void StartReport(string title)
		{
			_rep.WriteLine("<!DOCTYPE html>");
			_rep.WriteLine("<html>");

			_rep.WriteLine("<head>");
			if (!string.IsNullOrEmpty(title)) _rep.WriteLine(string.Format("<title>{0}</title>", HttpUtility.HtmlEncode(title)));

			_rep.WriteLine(@"<style type=""text/css"">
h1 { font-family: ""Segoe UI"", Verdana, sans-serif; color: #ffffff; }
body { font-family: ""Segoe UI"", Verdana, sans-serif; font-size: .8em; background-color: #222222; color: #cccccc; }
.headerTable { padding: 0px 0px 0px 0px; /*width: 100%;*/ }
.headerItem { border: 1px solid #000000; padding: 0px 0px 0px 0px; }
.headerLabel { font-weight: bold; background-color: #333333; color: #ffffff; width: 200px; }
.headerValue { border: 1px solid #000000; }
.infoItem { }
.errorItem { color: #ffffff; background-color: #ff0000; font-weight: bold; border: 1px solid #cccccc; margin-top: 4px; margin-bottom: 4px; padding-left: 4px; }
.errorException { font-family: Consolas, Courier New; color: #ffeeee; }
.warningItem { color: #ffffff; background-color: #ff8800; font-weight: bold; border: 1px solid #cccccc; margin-top: 4px; margin-bottom: 4px; padding-left: 4px; }
.section { border: solid 1px #000000; margin: 2px 0px 2px 0px; }
.sectionTitle { font-weight: bold; font-size: 1.2em; margin: 2px 2px 2px 2px; }
.opTable { }
.opHeader { font-weight: bold; font-size: .9em; }
.opRow { font-size: .9em; }
.opRow td { background-color: #333333; padding-left: 4px; padding-top: 0px; padding-right: 4px; padding-bottom: 0px; }
.opDetail { font-weight: normal; display: inline; }
.opAction { }
.opPath { }
.opSize { }
.opReason { }
.noChanges { font-size: .9em; }
.syncStart { background-color: #222222; }
.data { font-family: Consolas, Courier New; background-color: #333333; }
.summaryTable { padding: 0px 0px 0px 0px; /*width: 100%;*/ }
.summaryItem { border: 1px solid #000000; padding: 0px 0px 0px 0px; }
.summaryLabel { font-weight: bold; background-color: #333333; color: #cccccc; width: 200px; }
.summaryValue { border: 1px solid #000000; background-color: #333333; color: #cccccc; }
</style>");

			_rep.WriteLine("</head>");

			_rep.WriteLine("<body>");
			if (!string.IsNullOrEmpty(title)) _rep.WriteLine(string.Format("<h1>{0}</h1>", HttpUtility.HtmlEncode(title)));
			
			_started = true;
		}

		public void StartHeader()
		{
			_rep.WriteLine("<table class=\"headerTable\">");
		}

		public void WriteHeader(string label, string value)
		{
			_rep.Write("<tr class=\"headerItem\"><td class=\"headerLabel\">");
			_rep.Write(HttpUtility.HtmlEncode(label));
			_rep.Write("</td><td class=\"headerValue data\">");
			_rep.Write(HttpUtility.HtmlEncode(value));
			_rep.WriteLine("</td></tr>");
		}

		public void EndHeader()
		{
			_rep.WriteLine("</table>");
		}

		public void WriteInfo(string message)
		{
			_rep.Write("<div class=\"infoItem\">");
			_rep.Write(HttpUtility.HtmlEncode(message));
			_rep.WriteLine("</div>");
		}

		public void WriteError(Exception ex, string message)
		{
			_rep.Write("<div class=\"errorItem\">");
			if (!string.IsNullOrWhiteSpace(message))
			{
				_rep.Write(HttpUtility.HtmlEncode(message));
				_rep.Write("<br/>");
			}
			_rep.Write("<span class=\"errorException\">");
			_rep.Write(HttpUtility.HtmlEncode(ex.ToString()));
			_rep.WriteLine("</span></div>");
		}

		public void WriteError(string message)
		{
			_rep.Write("<div class=\"errorItem\">");
			if (!string.IsNullOrWhiteSpace(message)) _rep.Write(HttpUtility.HtmlEncode(message));
			_rep.WriteLine("</div>");
		}

		public void WriteWarning(string message)
		{
			_rep.Write("<div class=\"warningItem\">");
			_rep.Write(HttpUtility.HtmlEncode(message));
			_rep.WriteLine("</div>");
		}

		public void WriteFileOperation(string action, string relPath, long? size, string reason)
		{
			if (!_sectionHeaders)
			{
				_rep.Write("<table class=\"opTable\">");
				_rep.WriteLine("<tr class=\"opHeader\"><td>Action</td><td>Path</td><td>Size</td><td>Reason</td></tr>");
				_sectionHeaders = true;
			}
			_rep.Write("<tr class=\"opRow\"><td class=\"opAction\">");
			_rep.Write(HttpUtility.HtmlEncode(action));
			_rep.Write("</td><td class=\"opPath\">");
			_rep.Write(HttpUtility.HtmlEncode(relPath));
			_rep.Write("</td><td class=\"opSize\">");
			if (size.HasValue) _rep.Write(HttpUtility.HtmlEncode(size.Value.FormatSize()));
			_rep.Write("</td><td class=\"opReason\">");
			_rep.Write(HttpUtility.HtmlEncode(reason));
			_rep.WriteLine("</td></tr>");

			_sectionContent = true;
		}

		public void StartSection(string title)
		{
			_rep.Write("<div class=\"section\">");
			_rep.Write("<div class=\"sectionTitle\">");
			_rep.Write(HttpUtility.HtmlEncode(title));
			_rep.WriteLine("</div>");

			_sectionContent = false;
			_sectionHeaders = false;
		}

		public void EndSection()
		{
			if (_sectionContent)
			{
				_rep.WriteLine("</table>");
			}
			else
			{
				_rep.WriteLine("<div class=\"noChanges\">(no changes)</div>");
			}

			_rep.WriteLine("</div>");
		}

		public void StartSummary()
		{
			_rep.WriteLine("<table class=\"summaryTable\">");
		}

		public void WriteSummary(string label, string value)
		{
			_rep.Write("<tr class=\"summaryItem\"><td class=\"summaryLabel\">");
			_rep.Write(HttpUtility.HtmlEncode(label));
			_rep.Write("</td><td class=\"summaryValue data\" colspan=\"2\">");
			_rep.Write(HttpUtility.HtmlEncode(value));
			_rep.WriteLine("</td></tr>");
		}

		public void WriteSummary(string label, string leftValue, string rightValue)
		{
			_rep.Write("<tr class=\"summaryItem\"><td class=\"summaryLabel\">");
			_rep.Write(HttpUtility.HtmlEncode(label));
			_rep.Write("</td><td class=\"summaryValue data\">");
			_rep.Write(HttpUtility.HtmlEncode(leftValue));
			_rep.Write("</td><td class=\"summaryValue data\">");
			_rep.Write(HttpUtility.HtmlEncode(rightValue));
			_rep.WriteLine("</td></tr>");
		}

		public void EndSummary()
		{
			_rep.WriteLine("</table>");
		}
	}
}

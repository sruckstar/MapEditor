using GTA;
using LemonUI.Scaleform;

namespace MapEditor
{
    public static class ObjectPreview
    {
        private static InstructionalButtons _loadingButtons;

        public static Model LoadObject(int hash)
        {
            int counter = 0;

            if (hash == 0) return null;

            var m = new Model(hash);

	        // Only disown the model when both natives agree it does not exist. IS_MODEL_VALID and
	        // IS_MODEL_IN_CDIMAGE do not agree on every build for streamed-in interior and DLC props, and
	        // requiring both is what put thousands of loadable props into InvalidObjects.ini.
	        if (!m.IsValid && !m.IsInCdImage)
	        {
		        ObjectDatabase.MarkHashInvalid(hash);
		        return null;
	        }

            if (_loadingButtons == null)
            {
                // LemonUI's scaleforms are Visible = false by default and Draw() is a no-op until it's set.
                _loadingButtons = new InstructionalButtons(
                    new InstructionalButton(Translation.Translate("Loading Model"), "b_50"))
                {
                    Visible = true,
                };
                _loadingButtons.Update();
            }

            while (!m.IsLoaded && counter < 200)
			{
                m.Request();
                Script.Yield();
                counter++;
                _loadingButtons.Draw();
            }

	        if (!m.IsLoaded)
	        {
		        // The streamer was busy or the model is genuinely unloadable. Either way this is not proof
		        // that the model is bad, so report the failure without blacklisting it.
		        m.MarkAsNoLongerNeeded();
		        return null;
	        }

	        ObjectDatabase.MarkHashValid(hash);
            return m;
        }
    }
}

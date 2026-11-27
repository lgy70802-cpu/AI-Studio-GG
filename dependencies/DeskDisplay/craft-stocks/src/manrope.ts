let loading: Promise<void> | undefined;

export function loadManrope() {
  if (loading) return loading;
  loading = new FontFace('ManropeTabular', 'url("fonts/manrope.ttf")', { weight: '200 800', style: 'normal', featureSettings: '"tnum" 1' })
    .load()
    .then((font) => {
      document.fonts.add(font);
    })
    .catch((error) => {
      console.warn(JSON.stringify({ event: 'font_load_failed', font: 'Manrope', message: String(error) }));
    });
  return loading;
}

void loadManrope();

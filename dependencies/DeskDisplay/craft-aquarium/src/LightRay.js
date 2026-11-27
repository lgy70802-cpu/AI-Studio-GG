export class LightRay {
  constructor(index, config) {
    this.x = config.designWidth * (0.18 + index * 0.205);
    this.width = 90 + index * 12; this.height = config.designHeight * 0.77;
    this.phase = index * 1.83; this.baseOpacity = 0.065; this.speed = 0.12 + index * 0.015;
    this.top = config.designHeight * 0.085;
  }
  draw(ctx, time) {
    ctx.save(); ctx.globalCompositeOperation = 'screen';
    ctx.globalAlpha = this.baseOpacity + Math.sin(time * this.speed + this.phase) * 0.025;
    ctx.translate(this.x, this.top);
    ctx.rotate(Math.sin(time * 0.06 + this.phase) * Math.PI / 180);
    ctx.drawImage(this.sprite, -this.width, 0, this.width * 2, this.height);
    ctx.restore();
  }
}

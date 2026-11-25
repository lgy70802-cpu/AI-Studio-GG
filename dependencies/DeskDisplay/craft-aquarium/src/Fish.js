const DEPTH = {
  back: { scale: [0.45, 0.7], opacity: [0.4, 0.65], speed: [15, 25], blur: 0.8 },
  mid: { scale: [0.7, 1], opacity: [0.75, 1], speed: [20, 35], blur: 0 },
  front: { scale: [1, 1.35], opacity: [0.85, 1], speed: [28, 45], blur: 1 }
};
export class Fish {
  constructor(id, depth, image, config, random = Math.random) {
    this.id = id; this.depth = depth; this.image = image; this.random = random;
    const d = DEPTH[depth], range = (a, b) => a + random() * (b - a);
    const scale = range(...d.scale);
    this.width = 128 * scale;
    this.height = this.width * (image.height / image.width);
    this.opacity = range(...d.opacity); this.blur = d.blur;
    this.direction = random() < 0.5 ? -1 : 1;
    this.facing = this.direction;
    this.minSpeed = d.speed[0]; this.maxSpeed = d.speed[1];
    this.speed = this.targetSpeed = range(...d.speed);
    this.amplitude = range(4, 12); this.frequency = range(0.3, 0.8);
    this.phase = range(0, Math.PI * 2); this.rotation = 0;
    this.margin = config.designWidth * 0.04 + this.width / 2;
    this.right = config.designWidth - this.margin;
    this.x = this.margin + (this.right - this.margin) * ((id * 0.137 + 0.09) % 1);
    const low = Math.max(config.designHeight * 0.20, this.height / 2 + this.amplitude + 60);
    const high = Math.max(low, config.designHeight * 0.83 - this.height / 2 - this.amplitude);
    this.baseY = low + (high - low) * ((id * 0.381 + 0.12) % 1);
    this.y = this.baseY; this.state = 'swimming'; this.turnTime = 0;
    this.nextSpeed = range(5, 12); this.entering = config.spawnFromEdges;
    if (this.entering) this.x = this.direction === 1 ? -this.width : config.designWidth + this.width;
    this.naturalMotion = config.naturalMotion;
    this.lowY = low; this.highY = high;
    this.routeFrom = this.routeTo = this.baseY; this.routeTime = 0; this.routeDuration = range(6, 14);
    this.nextHover = range(20, 38); this.hoverTime = 0;
    this.swimPhase = this.phase;
  }
  update(dt, time, speedMultiplier = 1) {
    this.nextSpeed -= dt;
    if (this.nextSpeed <= 0) {
      this.targetSpeed = this.minSpeed + this.random() * (this.maxSpeed - this.minSpeed);
      this.nextSpeed = 5 + this.random() * 7;
    }
    if (this.entering && this.x >= this.margin && this.x <= this.right) this.entering = false;
    if (this.naturalMotion) {
      this.routeTime += dt;
      if (this.routeTime >= this.routeDuration) {
        this.routeFrom = this.baseY;
        this.routeTo = Math.max(this.lowY, Math.min(this.highY, this.baseY + (this.random() * 2 - 1) * 75));
        this.routeTime = 0; this.routeDuration = 12 + this.random() * 10;
      }
      const u = Math.min(1, this.routeTime / this.routeDuration);
      // Quintic easing gives zero vertical velocity/acceleration at route joins.
      this.baseY = this.routeFrom + (this.routeTo - this.routeFrom) * (u * u * u * (10 + u * (-15 + u * 6)));
      this.nextHover -= dt;
      if (this.nextHover <= 0 && this.state === 'swimming' && !this.entering && this.x > this.margin + 100 && this.x < this.right - 100) {
        this.state = 'hovering'; this.hoverTime = 1.8 + this.random() * 1.5;
        this.nextHover = 22 + this.random() * 20;
      }
    }
    if (this.state !== 'turning' && !this.entering &&
        ((this.direction > 0 && this.x >= this.right - 24) || (this.direction < 0 && this.x <= this.margin + 24))) {
      this.state = 'turning'; this.turnTime = 0; this.turnDuration = 0.5 + this.random() * 0.3;
      this.turnSpeed = this.speed; this.oldDirection = this.direction;
    }
    if (this.state === 'turning') {
      this.turnTime += dt;
      const p = Math.min(1, this.turnTime / this.turnDuration);
      this.speed = this.turnSpeed * Math.max(0, 1 - p / 0.4);
      // Hold briefly, then rotate through an edge-on silhouette instead of snapping.
      const q = Math.min(1, Math.max(0, (p - 0.28) / 0.72));
      this.facing = this.oldDirection * Math.cos(Math.PI * q);
      if (p >= 1) { this.direction = -this.oldDirection; this.facing = this.direction; this.state = 'accelerating'; }
    } else {
      if (this.state === 'hovering') {
        this.hoverTime -= dt;
        if (this.hoverTime <= 0) this.state = 'accelerating';
      }
      const desired = this.state === 'hovering' ? this.targetSpeed * 0.14 : this.targetSpeed;
      this.speed += (desired - this.speed) * (1 - Math.exp(-dt * 1.6));
      if (this.state === 'accelerating' && Math.abs(this.targetSpeed - this.speed) < 1) this.state = 'swimming';
    }
    this.x += this.speed * this.direction * dt * speedMultiplier;
    if (!this.entering) this.x = Math.max(this.margin, Math.min(this.right, this.x));
    const previousY = this.y;
    this.y = this.baseY + Math.sin(time * this.frequency + this.phase) * this.amplitude;
    const slope = (this.y - previousY) / Math.max(dt, 0.001) / Math.max(18, this.speed);
    const pitch = Math.max(-0.11, Math.min(0.11, slope * 0.45)) * this.direction;
    this.rotation += (pitch - this.rotation) * (1 - Math.exp(-dt * 2));
    this.swimPhase = (this.swimPhase + dt * (1.5 + this.speed / this.maxSpeed * 3.2)) % (Math.PI * 2);
  }
  draw(ctx, time) {
    ctx.save(); ctx.globalAlpha = this.opacity;
    ctx.translate(this.x, this.y); ctx.rotate(this.rotation);
    ctx.scale(this.facing, 1);
    const sprite = this.sprite || this.image, w = this.width, h = this.height;
    if (this.spriteFrames) {
      const position = this.swimPhase / (Math.PI * 2) * this.spriteFrames.length;
      const index = Math.floor(position), mix = position - index;
      // Adjacent cached frames blend in a reusable small canvas, preserving alpha.
      const blend = this.blendContext, canvas = this.blendCanvas;
      blend.clearRect(0, 0, canvas.width, canvas.height);
      blend.globalCompositeOperation = 'source-over'; blend.globalAlpha = 1 - mix;
      blend.drawImage(this.spriteFrames[index], 0, 0);
      blend.globalCompositeOperation = 'lighter'; blend.globalAlpha = mix;
      blend.drawImage(this.spriteFrames[(index + 1) % this.spriteFrames.length], 0, 0);
      ctx.drawImage(canvas, -w * 0.56, -h * 0.60, w * 1.12, h * 1.20);
    } else ctx.drawImage(sprite, -w / 2, -h / 2, w, h);
    ctx.restore();
  }
}
